using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackSceneRender(GraphicsCore _graphicsCore)
{
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore;
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

	private static readonly RenderMode[] renderPassModes =
	[
		RenderMode.Opaque,
		RenderMode.Transparent,
		//...
	];

	#endregion
	#region Methods

	public bool DrawAllSceneCameras(
		in SceneContext _sceneCtx,
		Scene _scene,
		in List<IRenderer> _renderers,
		in IList<CameraComponent> _cameras,
		in IList<ILightSource> _lights,
		uint _lightCount,
		uint _lightCountShadowMapped)
	{
		List<CameraComponent> activeCameras = _cameras.Where(o => !o.IsDisposed && o.layerMask != 0 && o.node.IsEnabledInHierarchy()).ToList();
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
				renderPassModes,
				_lightCount,
				_lightCountShadowMapped);
		}

		return success;
	}

	private bool DrawSceneCamera(
		in SceneContext _sceneCtx,
		in CameraComponent _camera,
		uint _cameraIdx,
		in List<IRenderer> _renderers,
		RenderMode[] _renderPassModes,
		uint _lightCount,
		uint _lightCountShadowMapped)
	{
		if (!graphicsCore.CreateCommandList(out CommandList? cmdList))
		{
			logger.LogError("Failed to create command list for drawing scene camera!");
			return false;
		}

		//TODO: Sort renderers by render mode!

		if (!_camera.BeginFrame(
			_lightCount,
			out bool rebuildResSetCamera))
		{
			logger.LogError("Failed to begin drawing camera frame!");
			return false;
		}

		bool success = true;

		for (int passIdx = 0; passIdx < _renderPassModes.Length; passIdx++)
		{
			RenderMode passRenderMode = _renderPassModes[passIdx];

			if (!_camera.BeginPass(
				in _sceneCtx,
				cmdList!,
				passRenderMode,
				true,				//TODO: Only set this true for first pass, or passes with isolated render targets!
				_cameraIdx,
				_lightCount,
				_lightCountShadowMapped,
				out CameraPassContext cameraPassCtx,
				rebuildResSetCamera))
			{
				success = false;
				break;
			}

			foreach (IRenderer renderer in _renderers)
			{
				if (renderer.RenderMode != passRenderMode) continue;    //TODO / TEMP

				success &= renderer.Draw(_sceneCtx, cameraPassCtx);
			}

			success &= _camera.EndFrame();
		}

		success &= _camera.EndFrame();

		if (success)
		{
			success &= graphicsCore.CommitCommandList(cmdList!);
		}
		return success;
	}

	#endregion
}
