using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.PostProcessing;
using Veldrid;

namespace FragEngine3.Graphics.Stack.ForwardPlusLights;

internal sealed class ForwardPlusLightsSceneRender(ForwardPlusLightsStack _stack, ForwardPlusLightsSceneObjects _sceneObjects, ForwardPlusLightsComposition _composition)
{
	#region Fields

	private readonly GraphicsCore core = _stack.Core;
	private readonly Logger logger = _stack.Core.graphicsSystem.Engine.Logger;

	private readonly ForwardPlusLightsStack stack = _stack;
	private readonly ForwardPlusLightsSceneObjects sceneObjects = _sceneObjects;
	private readonly ForwardPlusLightsComposition composition = _composition;

	#endregion
	#region Methods

	public bool DrawSceneCameras(in SceneContext _sceneCtx, bool _rebuildAllResSetCamera)
	{
		bool success = true;

		// Gather light data for each active light source:
		sceneObjects.PrepareLightSourceData();

		// Fetch or create a command list for shadow rendering:
		if (!stack.GetOrCreateCommandList(out CommandList cmdList))
		{
			return false;
		}
		cmdList.Begin();

		List<ILightSource> visibleLights = new((int)sceneObjects.ActiveLightCount);

		for (uint i = 0; i < sceneObjects.ActiveCamerasCount; ++i)
		{
			CameraComponent camera = sceneObjects.activeCameras[(int)i];

			// Pre-filter lights to only include those are actually visible by current camera:
			visibleLights.Clear();
			foreach (ILightSource light in sceneObjects.activeLights)
			{
				if (light.CheckVisibilityByCamera(in camera))
				{
					visibleLights.Add(light);
				}
			}
			uint visibleLightCount = (uint)visibleLights.Count;

			// Try drawing the camera's frame:
			try
			{
				if (!camera.BeginFrame(visibleLightCount, out bool bufLightsChanged))
				{
					success = false;
					continue;
				}

				bool rebuildResSetCamera = _rebuildAllResSetCamera || bufLightsChanged;
				bool result = true;

				result &= camera.SetOverrideCameraTarget(null);

				// Upload per-camera light data to GPU buffer:
				for (uint j = 0; j < visibleLightCount; ++j)
				{
					camera.LightDataBuffer.SetLightData(j, in sceneObjects.activeLightData[j]);
				}
				camera.LightDataBuffer.FinalizeBufLights(cmdList);

				// Draw scene geometry and UI passes:
				result &= DrawSceneRenderers(in _sceneCtx, cmdList, camera, RenderMode.Opaque, sceneObjects.activeRenderersOpaque, true, rebuildResSetCamera, i);
				result &= DrawSceneRenderers(in _sceneCtx, cmdList, camera, RenderMode.Transparent, sceneObjects.activeRenderersTransparent, false, rebuildResSetCamera, i);
				result &= DrawSceneRenderers(in _sceneCtx, cmdList, camera, RenderMode.UI, sceneObjects.activeRenderersUI, false, rebuildResSetCamera, i);

				// Scene compositing:
				Framebuffer sceneFramebuffer = null!;           //TODO [later]: Add all-in-one compositing option, in case no post-processing is needed on scene render.
				if (result)
				{
					result &= composition.CompositeSceneOutput(
						in _sceneCtx,
						camera,
						rebuildResSetCamera,
						i,
						out sceneFramebuffer);
				}

				// Post-processing on scene render:
				if (result && stack.PostProcessingStackScene is not null)
				{
					result &= DrawPostProcessingStack(
						in _sceneCtx,
						in cmdList,
						in sceneFramebuffer,
						camera,
						stack.PostProcessingStackScene,
						RenderMode.PostProcessing_Scene,
						_rebuildAllResSetCamera,
						i,
						out sceneFramebuffer);
				}

				// UI compositing:
				Framebuffer finalFramebuffer = null!;
				if (result)
				{
					result &= composition.CompositeFinalOutput(
						in _sceneCtx,
						in sceneFramebuffer,
						camera,
						rebuildResSetCamera,
						i,
						out finalFramebuffer);
				}

				// Post-processing on final image:
				if (result && stack.PostProcessingStackFinal is not null)
				{
					result &= DrawPostProcessingStack(
						in _sceneCtx,
						in cmdList,
						in finalFramebuffer,
						camera,
						stack.PostProcessingStackFinal,
						RenderMode.PostProcessing_PostUI,
						_rebuildAllResSetCamera,
						i,
						out _);
				}

				result &= camera.EndFrame();
				success &= result;
			}
			catch (Exception ex)
			{
				logger.LogException($"An unhandled exception was caught while drawing scene camera {i}!", ex);
				success = false;
				break;
			}
		}

		// If any shadows maps were rendered, submit command list for execution:
		cmdList.End();
		success &= core.CommitCommandList(cmdList);

		return success;
	}

	private bool DrawSceneRenderers(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		CameraComponent _camera,
		RenderMode _renderMode,
		in List<IRenderer> _renderers,
		bool _clearRenderTargets,
		bool _rebuildResSetCamera,
		uint _cameraIdx)
	{
		if (!_camera.BeginPass(
			in _sceneCtx,
			_cmdList,
			_renderMode,
			_clearRenderTargets,
			_cameraIdx,
			sceneObjects.ActiveLightCount,
			sceneObjects.ActiveShadowMappedLightsCount,
			out CameraPassContext cameraPassCtx,
			_rebuildResSetCamera))
		{
			return false;
		}

		bool success = true;

		foreach (IRenderer renderer in _renderers)
		{
			if ((_camera.layerMask & renderer.LayerFlags) != 0)
			{
				success &= renderer.Draw(_sceneCtx, cameraPassCtx);         //TODO [important]: Change this to not crash the game if a single renderer fails to draw!
			}
		}

		success &= _camera.EndPass();
		return success;
	}

	private bool DrawPostProcessingStack(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		in Framebuffer _inputFramebuffer,
		CameraComponent _camera,
		IPostProcessingStack _postProcessingStack,
		RenderMode _renderMode,
		bool _rebuildResSetCamera,
		uint _cameraIdx,
		out Framebuffer _outResultFramebuffer)
	{
		// if post-processing stack is unavailable, try continuing with unchanged input framebuffer:
		if (_postProcessingStack is null || _postProcessingStack.IsDisposed)
		{
			_outResultFramebuffer = _inputFramebuffer;
			return true;
		}

		if (!_camera.BeginPass(
			in _sceneCtx,
			_cmdList,
			_renderMode,
			true,
			_cameraIdx,
			sceneObjects.ActiveLightCount,
			sceneObjects.ActiveShadowMappedLightsCount,
			out CameraPassContext cameraCtx,
			_rebuildResSetCamera))
		{
			_outResultFramebuffer = _inputFramebuffer;
			return false;
		}

		// If this is the main camera, and we're on operating on final image, ouput results directly to the swapchain's backbuffer:
		if (_camera.IsMainCamera && _renderMode == RenderMode.PostProcessing_PostUI)
		{
			Framebuffer backbuffer = core.Device.SwapchainFramebuffer;
			if (!_camera.SetOverrideCameraTarget(backbuffer, false))
			{
				logger.LogError("Failed to set override render targets for graphics stack's scene composition pass!");
				_outResultFramebuffer = _inputFramebuffer;
				return false;
			}
		}

		return _postProcessingStack.Draw(in _sceneCtx, in cameraCtx, in _inputFramebuffer, out _outResultFramebuffer);
	}

	#endregion
}
