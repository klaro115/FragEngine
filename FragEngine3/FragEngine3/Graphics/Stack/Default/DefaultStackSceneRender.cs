using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Lighting.Data;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackSceneRender(GraphicsCore _graphicsCore) : IDisposable
{
	#region Constructors

	~DefaultStackSceneRender()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Types

	private sealed class PassRendererLists(int _initialCapacity)
	{
		public readonly List<IRenderer> opaqueList = new(_initialCapacity);
		public readonly List<IRenderer> transparentList = new(_initialCapacity);
		public readonly List<IRenderer> volumetricList = new(_initialCapacity);
		//...

		public int TotalRendererCount => opaqueList.Count + transparentList.Count + volumetricList.Count;

		public void Clear()
		{
			opaqueList.Clear();
			transparentList.Clear();
			volumetricList.Clear();
		}

		public void Add(IRenderer _renderer)
		{
			List<IRenderer>? targetList = _renderer.RenderMode switch
			{
				RenderMode.Opaque => opaqueList,
				RenderMode.Transparent => transparentList,
				RenderMode.Volumetric => volumetricList,
				_ => null,
			};
			targetList?.Add(_renderer);
		}
	}

	#endregion
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore;
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

	private readonly Stack<CommandList> commandListPool = new(4);
	private readonly Stack<CommandList> commandListsInUse = new(4);
	private readonly Stack<PassRendererLists> rendererListPool = new(4);
	private readonly PassRendererLists emptyRendererList = new(0);

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	private void Dispose(bool _)
	{
		IsDisposed = true;

		while (commandListPool.TryPop(out CommandList? cmdList))
		{
			cmdList?.Dispose();
		}
		while (commandListsInUse.TryPop(out CommandList? cmdList))
		{
			cmdList?.Dispose();
		}
		commandListPool.Clear();
		commandListsInUse.Clear();
	}

	public void Reset()
	{
		rendererListPool.Clear();
		emptyRendererList.Clear();

		while (commandListsInUse.TryPop(out CommandList? cmdList))
		{
			commandListPool.Push(cmdList);
		}
	}

	public bool DrawAllSceneCameras(
		in SceneContext _sceneCtx,
		//Scene _scene,						//TEMP: Scene reference might be needed later, once spatial partitioning has been added.
		in List<IRenderer> _renderers,
		in IList<CameraComponent> _cameras,
		in IList<ILightSource> _lights,
		uint _lightCount,
		uint _lightCountShadowMapped,
		out bool _outRebuildResSetCamera)
	{
		_outRebuildResSetCamera = false;
		if (IsDisposed)
		{
			logger.LogError("Cannot draw scene cameras using graphics stack scene renderer module that is disposed!");
			return false;
		}

		while (commandListsInUse.TryPop(out CommandList? cmdList))
		{
			commandListPool.Push(cmdList);
		}

		List<CameraComponent> activeCameras = _cameras.Where(static o => !o.IsDisposed && o.layerMask != 0 && o.node.IsEnabledInHierarchy()).ToList();
		if (activeCameras.Count == 0)
		{
			logger.LogWarning("Scene contains no active cameras, cannot draw graphics stack.");
			return true;
		}

		bool success = true;

		for (int cameraIdx = 0; cameraIdx < activeCameras.Count; cameraIdx++)
		{
			CameraComponent camera = activeCameras[cameraIdx];
			success &= DrawSceneCamera(
				in _sceneCtx,
				in camera,
				(uint)cameraIdx,
				in _renderers,
				in _lights,
				_lightCount,
				_lightCountShadowMapped,
				out bool rebuildResSetCamera);
			_outRebuildResSetCamera |= rebuildResSetCamera;
		}

		return success;
	}

	private bool DrawSceneCamera(
		in SceneContext _sceneCtx,
		in CameraComponent _camera,
		uint _cameraIdx,
		in List<IRenderer> _renderers,
		in IList<ILightSource> _lights,
		uint _totalLightCount,
		uint _totalLightCountShadowMapped,
		out bool _outRebuildResSetCamera)
	{
		// Identify visible renderers, and sort them by render mode:
		if (!GetRenderersVisibleToCamera(in _camera, in _renderers, out PassRendererLists? visibleRenderers))
		{
			logger.LogError($"Failed to identify renderers that are visible by scene camera! Camera: '{_camera}'");
			_outRebuildResSetCamera = false;
			return false;
		}

		if (!GetOrCreateCommandList(out CommandList? cmdList))
		{
			logger.LogError("Failed to create command list for drawing scene camera!");
			AbortUsingCommandList(cmdList!);
			_outRebuildResSetCamera = false;
			return false;
		}

		// Identify visible lights, and register them in the camera's 'BufLights' buffer:
		if (!ProcessLightsVisibleToCamera(in cmdList!, in _camera, in _lights, out uint visibleLightCount, out uint visibleLightCountShadowMapped, out bool recreatedBufLights))
		{
			logger.LogError($"Failed to identify light sources that are visible by scene camera! Camera: '{_camera}'");
			AbortUsingCommandList(cmdList!);
			_outRebuildResSetCamera = recreatedBufLights;
			return false;
		}

		if (!_camera.BeginFrame(
			_totalLightCount,
			out _outRebuildResSetCamera))
		{
			logger.LogError("Failed to begin drawing camera frame!");
			AbortUsingCommandList(cmdList!);
			return false;
		}
		_outRebuildResSetCamera |= recreatedBufLights;

		bool success = true;

		if (visibleRenderers is not null)
		{
			success &= _camera.SetOverrideCameraTarget(null);

			// 1. Opaque geometry:
			if (success)
			{
				success &= DrawRenderPass(
					in _sceneCtx,
					in cmdList!,
					in visibleRenderers.opaqueList,
					in _camera,
					_cameraIdx,
					RenderMode.Opaque,
					true,
					visibleLightCount,
					visibleLightCountShadowMapped,
					_outRebuildResSetCamera);
			}
			// 2. Transparent geometry:
			if (success && visibleRenderers.transparentList.Count != 0)
			{
				success &= DrawRenderPass(
					in _sceneCtx,
					in cmdList!,
					in visibleRenderers.transparentList,
					in _camera,
					_cameraIdx,
					RenderMode.Transparent,
					false,
					visibleLightCount,
					visibleLightCountShadowMapped,
					_outRebuildResSetCamera);
			}
			// 3. Opaque geometry:
			if (success && visibleRenderers.volumetricList.Count != 0)
			{
				success &= DrawRenderPass(
					in _sceneCtx,
					in cmdList!,
					in visibleRenderers.volumetricList,
					in _camera,
					_cameraIdx,
					RenderMode.Volumetric,
					false,
					visibleLightCount,
					visibleLightCountShadowMapped,
					_outRebuildResSetCamera);
			}
			//...

			// Return renderer lists to pool for later re-use:
			visibleRenderers.Clear();
			rendererListPool.Push(visibleRenderers);
		}
		else
		{
			// No geometry, just clear render targets:
			success &= DrawRenderPass(
				in _sceneCtx,
				in cmdList!,
				in emptyRendererList.opaqueList,
				in _camera,
				_cameraIdx,
				RenderMode.Opaque,
				true,
				_totalLightCount,
				_totalLightCountShadowMapped,
				_outRebuildResSetCamera);
		}

		if (success)
		{
			success &= _camera.EndFrame();
		}

		if (success)
		{
			success &= graphicsCore.CommitCommandList(cmdList!);
		}

		cmdList!.End();
		commandListsInUse.Push(cmdList!);
		return success;
	}

	private static bool DrawRenderPass(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		in List<IRenderer> _renderers,
		in CameraComponent _camera,
		uint _cameraIdx,
		RenderMode _renderMode,
		bool _isFirstPass,
		uint _lightCount,
		uint _lightCountShadowMapped,
		bool _rebuildResSetCamera)
	{
		// Begin drawing, clear render targets if needed:
		if (!_camera.BeginPass(
			in _sceneCtx,
			_cmdList!,
			_renderMode,
			_isFirstPass,
			_cameraIdx,
			_lightCount,
			_lightCountShadowMapped,
			out CameraPassContext cameraPassCtx,
			_rebuildResSetCamera))
		{
			return false;
		}

		bool succes = true;

		// Render objects in the scene:
		foreach (IRenderer renderer in _renderers)
		{
			succes &= renderer.Draw(_sceneCtx, cameraPassCtx);
		}

		// End frame:
		succes &= _camera.EndPass();
		return succes;
	}

	private bool GetOrCreateCommandList(out CommandList? _outCmdList)
	{
		bool result;
		if (!(result = commandListPool.TryPop(out _outCmdList)))
		{
			result = graphicsCore.CreateCommandList(out _outCmdList);
		}

		if (result)
		{
			_outCmdList!.Begin();
		}
		return result;
	}

	private void AbortUsingCommandList(CommandList _cmdList)
	{
		if (_cmdList is null || _cmdList.IsDisposed) return;

		_cmdList.End();
		commandListPool.Push(_cmdList);
	}

	private bool GetRenderersVisibleToCamera(in CameraComponent _camera, in List<IRenderer> _allRenderers, out PassRendererLists? _outVisibleRenderers)
	{
		_outVisibleRenderers = null;
		if (!_camera.node.IsEnabled || _camera.layerMask == 0)
		{
			return true;
		}
		if (_allRenderers.Count == 0)
		{
			return true;
		}

		// Try to re-use renderer lists from a pool; allocate new ones if none are available:
		if (!rendererListPool.TryPop(out PassRendererLists? passRendererList))
		{
			int initialCapacity = Math.Max(_allRenderers.Count / 4, 32);
			passRendererList = new PassRendererLists(initialCapacity);
		}

		// Sort all renderers by render mode:
		foreach (IRenderer renderer in _allRenderers)   //TODO/TEMP [later]: Replace this logic with spatial partitioning lookup.
		{
			passRendererList.Add(renderer);
		}

		// Return results:
		if (passRendererList.TotalRendererCount != 0)
		{
			_outVisibleRenderers = passRendererList;
		}
		else
		{
			rendererListPool.Push(passRendererList);
		}
		return true;
	}

	private bool ProcessLightsVisibleToCamera(in CommandList _cmdList, in CameraComponent _camera, in IList<ILightSource> _allLights, out uint _outVisibleLightCount, out uint _outVisibleLightCountShadowMapped, out bool _outRecreatedBufLights)
	{
		_outVisibleLightCountShadowMapped = 0;
		if (_allLights.Count == 0)
		{
			_outVisibleLightCount = 0;
			_outRecreatedBufLights = false;
			return true;
		}

		bool success = true;

		// Identify all light sources that are active, and that will have an effect within visual range:
		List<ILightSource> visibleLights = new(_allLights.Count);
		foreach (ILightSource light in _allLights)
		{
			if (light.IsVisible && (light.LayerMask & _camera.layerMask) != 0)  //TODO/TEMP [later]: For non-directional lights, add spatial partitioning lookup here.
			{
				visibleLights.Add(light);
				if (light.CastShadows)
				{
					_outVisibleLightCountShadowMapped++;
				}
			}
		}
		_outVisibleLightCount = (uint)visibleLights.Count;

		if (!_camera.LightDataBuffer.PrepareBufLights(_outVisibleLightCount, out _outRecreatedBufLights))
		{
			return false;
		}

		// Gather GPU buffer data describing each of the visible light sources:
		for (int lightIdx = 0; lightIdx < visibleLights.Count; lightIdx++)
		{
			ILightSource light = visibleLights[lightIdx];
			LightSourceData data = light.GetLightSourceData();

			if (!_camera.LightDataBuffer.SetLightData((uint)lightIdx, in data))
			{
				success = false;
				break;
			}
		}

		// Return results and log any errors:
		if (!success)
		{
			logger.LogError($"Failed to gather light source data for scene camera render! (Camera: '{_camera}')");
		}
		else if (!_camera.LightDataBuffer.FinalizeBufLights(_cmdList))
		{
			logger.LogError($"Failed to finalize light data buffer for scene camera render! (Camera: '{_camera}')");
		}
		return success;
	}

	#endregion
}
