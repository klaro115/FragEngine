using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Utility;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackCameraRender(
	GraphicsCore _graphicsCore,
	DefaultStackSceneRender _sceneRenderStack,
	DefaultStackComposition _compositionStack,
	DefaultStackPostProcessing _postProcessingStack) : IDisposable
{
	#region Constructors

	~DefaultStackCameraRender()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore;
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

	private readonly DefaultStackSceneRender sceneRenderStack = _sceneRenderStack;
	private readonly DefaultStackComposition compositionStack = _compositionStack;
	private readonly DefaultStackPostProcessing postProcessingStack = _postProcessingStack;

	private readonly CommandListPool cmdListPool = new(_graphicsCore);

	private readonly List<CameraComponent> activeCameras = [];

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
		cmdListPool.Clear();
		cmdListPool.Dispose();
	}

	public void Reset()
	{
		cmdListPool.ReturnUsedToPool();
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

		cmdListPool.ReturnUsedToPool();

		activeCameras.Clear();
		activeCameras.AddWhere(_cameras, (camera) =>
		{
			bool isActive = !camera.IsDisposed && camera.layerMask != 0 && camera.node.IsEnabledInHierarchy();
			return isActive;
		});
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
		// Prepare command list and begin camera frame:
		if (!cmdListPool.GetOrCreateCommandList(out CommandList? cmdList))
		{
			logger.LogError("Failed to create command list for drawing scene camera!");
			AbortUsingCommandList(cmdList!);
			_outRebuildResSetCamera = false;
			return false;
		}
		cmdList!.Begin();

		if (!_camera.BeginFrame(
			_totalLightCount,
			out _outRebuildResSetCamera))
		{
			logger.LogError("Failed to begin drawing camera frame!");
			AbortUsingCommandList(cmdList!);
			return false;
		}

		bool success = true;

		// Draw all geometry in the scene:
		success &= sceneRenderStack.DrawSceneGeometry(
			in _sceneCtx,
			in _camera,
			_cameraIdx,
			cmdList,
			in _renderers,
			in _lights,
			_totalLightCount,
			_totalLightCountShadowMapped,
			ref _outRebuildResSetCamera);

		// Composite scene render:
		if (success)
		{
			success &= compositionStack.CompositeSceneOutput(
				in _sceneCtx,
				in _camera,
				_cameraIdx,
				cmdList,
				_totalLightCount,
				_totalLightCountShadowMapped,
				ref _outRebuildResSetCamera);
		}

		// Apply post-processing:
		if (success)
		{
			success &= postProcessingStack.ApplyScenePostProcessing();
		}

		// End camera frame:
		if (success)
		{
			success &= _camera.EndFrame();
		}

		cmdList!.End();
		if (success)
		{
			success &= graphicsCore.CommitCommandList(cmdList!);
		}
		return success;
	}

	private void AbortUsingCommandList(CommandList _cmdList)
	{
		if (_cmdList is null || _cmdList.IsDisposed) return;

		_cmdList.End();
		cmdListPool.ReturnCommandListToPool(_cmdList);
	}

	#endregion
}
