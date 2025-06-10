using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting.Data;
using FragEngine3.Utility;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackSceneRender(GraphicsCore _graphicsCore)
{
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

	private readonly Stack<PassRendererLists> rendererListPool = new(4);
	private readonly PassRendererLists emptyRendererList = new(0);

	private readonly List<ILightSource> visibleLights = [];

	#endregion
	#region Methods

	public void Reset()
	{
		rendererListPool.Clear();
		emptyRendererList.Clear();
		visibleLights.Clear();
	}

	public bool DrawSceneGeometry(
		in SceneContext _sceneCtx,
		in CameraComponent _camera,
		uint _cameraIdx,
		CommandList _cmdList,
		in List<IRenderer> _renderers,
		in IList<ILightSource> _lights,
		uint _totalLightCount,
		uint _totalLightCountShadowMapped,
		ref bool _outRebuildResSetCamera)
	{
		// Identify visible renderers, and sort them by render mode:
		if (!GetRenderersVisibleToCamera(in _camera, in _renderers, out PassRendererLists? visibleRenderers))
		{
			logger.LogError($"Failed to identify renderers that are visible by scene camera! Camera: '{_camera}'");
			_outRebuildResSetCamera = false;
			return false;
		}

		// Identify visible lights, and register them in the camera's 'BufLights' buffer:
		bool success = ProcessLightsVisibleToCamera(in _cmdList!, _camera, in _lights, out uint visibleLightCount, out uint visibleLightCountShadowMapped, out bool recreatedBufLights);
		_outRebuildResSetCamera |= recreatedBufLights;
		if (!success)
		{
			logger.LogError($"Failed to identify light sources that are visible by scene camera! Camera: '{_camera}'");
			return false;
		}

		if (visibleRenderers is not null)
		{
			success &= _camera.SetOverrideCameraTarget(null);

			// 1. Opaque geometry:
			if (success)
			{
				success &= DrawRenderPass(
					in _sceneCtx,
					in _cmdList!,
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
					in _cmdList!,
					in visibleRenderers.transparentList,
					in _camera,
					_cameraIdx,
					RenderMode.Transparent,
					true,
					visibleLightCount,
					visibleLightCountShadowMapped,
					_outRebuildResSetCamera);
			}
			// 3. Opaque geometry:
			if (success && visibleRenderers.volumetricList.Count != 0)
			{
				success &= DrawRenderPass(
					in _sceneCtx,
					in _cmdList!,
					in visibleRenderers.volumetricList,
					in _camera,
					_cameraIdx,
					RenderMode.Volumetric,
					true,
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
				in _cmdList!,
				in emptyRendererList.opaqueList,
				in _camera,
				_cameraIdx,
				RenderMode.Opaque,
				true,
				_totalLightCount,
				_totalLightCountShadowMapped,
				_outRebuildResSetCamera);
		}

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
			passRendererList.Add(renderer);             //TODO: Add Z-sorting for transparent and volumetric renderers!
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

	private bool ProcessLightsVisibleToCamera(in CommandList _cmdList, CameraComponent _camera, in IList<ILightSource> _allLights, out uint _outVisibleLightCount, out uint _outVisibleLightCountShadowMapped, out bool _outRecreatedBufLights)
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
		{
			uint visibleLightCountShadowMapped = 0u;
			visibleLights.Clear();
			visibleLights.AddWhere(_allLights, (light) =>
			{
				bool isVisible = light.IsVisible && (light.LayerMask & _camera.layerMask) != 0; //TODO/TEMP [later]: For non-directional lights, add spatial partitioning lookup here.
				if (isVisible && light.CastShadows)
				{
					visibleLightCountShadowMapped++;
				}
				return isVisible;
			});
			_outVisibleLightCountShadowMapped = visibleLightCountShadowMapped;
			_outVisibleLightCount = (uint)visibleLights.Count;
		}

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
