using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.PostProcessing;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Stack.ForwardPlusLights;
using FragEngine3.Graphics.Utility;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack;

public sealed class ForwardPlusLightsStack : IGraphicsStack		//TODO: This type is a convoluted and unmaintainable mess, needs intense cleaning up.
{
	#region Constructors

	public ForwardPlusLightsStack(GraphicsCore _core)
	{
		core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		shadowMaps = new(this, sceneObjects);
	}

	~ForwardPlusLightsStack()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	private bool isInitialized = false;
	private bool isDrawing = false;

	// Lists & object management:
	private readonly ForwardPlusLightsSceneObjects sceneObjects = new();

	// Global resources:
	private DeviceBuffer? cbScene = null;
	private CBScene cbSceneData = default;
	private readonly Stack<CommandList> commandListPool = new();
	private readonly Stack<CommandList> commandListsInUse = new();
	private ResourceLayout? resLayoutCamera = null;
	private ResourceLayout? resLayoutObject = null;
	private ushort sceneResourceVersion = 0;

	// Shadow maps:
	private readonly ForwardPlusLightsShadowMaps shadowMaps;

	// Output composition:
	private ResourceSet? compositeSceneResourceSet = null;
	private ResourceSet? compositeUIResourceSet = null;
	private StaticMeshRenderer? compositeSceneRenderer = null;
	private StaticMeshRenderer? compositeUIRenderer = null;

	// Postprocessing:
	private IPostProcessingStack? postProcessingStackScene = null;
	private IPostProcessingStack? postProcessingStackFinal = null;

	#endregion
	#region Constants

	public const string RESOURCE_KEY_FULLSCREEN_QUAD_MESH = "FullscreenQuad";
	public const string RESOURCE_KEY_COMPOSITE_SCENE_MATERIAL = "Mtl_ForwardPlusLight_CompositeScene";
	public const string RESOURCE_KEY_COMPOSITE_UI_MATERIAL = "Mtl_ForwardPlusLight_CompositeUI";
	public const string NODE_NAME_COMPOSITE_SCENE_RENDERER = "ForwardPlusLight_Composite_Scene";
	public const string NODE_NAME_COMPOSITE_UI_RENDERER = "ForwardPlusLight_Composite_UI";
	public const uint COMPOSITION_LAYER_MASK = 0x800000u;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsValid => !IsDisposed && core.IsInitialized && Scene != null && !Scene.IsDisposed;	//TODO
	public bool IsInitialized => !IsDisposed && isInitialized;
	public bool IsDrawing => IsInitialized && isDrawing;

	public Scene? Scene { get; private set; } = null;
	public GraphicsCore Core => core;

	public IPostProcessingStack? PostProcessingStackScene
	{
		get => postProcessingStackScene != null && !postProcessingStackScene.IsDisposed ? postProcessingStackScene : null;
		set { if (value == null || !value.IsDisposed) postProcessingStackScene = value; }
	}
	public IPostProcessingStack? PostProcessingStackFinal
	{
		get => postProcessingStackFinal != null && !postProcessingStackFinal.IsDisposed ? postProcessingStackFinal : null;
		set { if (value == null || !value.IsDisposed) postProcessingStackFinal = value; }
	}

	public int VisibleRendererCount { get; private set; } = 0;
	public int SkippedRendererCount { get; private set; } = 0;
	public int FailedRendererCount { get; private set; } = 0;

	private Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
	{
		if (isInitialized)
		{
			Shutdown();
		}

		IsDisposed = true;

		cbScene?.Dispose();
		resLayoutCamera?.Dispose();
		resLayoutObject?.Dispose();
		compositeSceneResourceSet?.Dispose();
		compositeUIResourceSet?.Dispose();

		shadowMaps.Dispose();

		postProcessingStackScene?.Dispose();
		postProcessingStackFinal?.Dispose();

		foreach (CommandList cmdList in commandListPool)
		{
			cmdList.Dispose();
		}
		foreach (CommandList cmdList in commandListsInUse)
		{
			cmdList.Dispose();
		}
		commandListPool.Clear();
		commandListsInUse.Clear();
	}

	public bool Initialize(Scene _scene)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot initialize disposed Forward+Lights graphics stack!");
			return false;
		}
		if (_scene == null || _scene.IsDisposed)
		{
			Logger.LogError("Cannot initialize graphics stack for null or disposed scene!");
			return false;
		}
		if (IsInitialized)
		{
			Logger.LogError("Cannot re-initialize graphics stack that has already been initialized; shut it down first!");
			return false;
		}

		Scene = _scene;

		VisibleRendererCount = 0;
		SkippedRendererCount = 0;
		FailedRendererCount = 0;

		// GLOBAL RESOURCES:

		if (!CameraUtility.CreateCameraResourceLayout(in core, out resLayoutCamera))
		{
			Logger.LogError("Failed to create default camera resource layout for graphics stack!");
			return false;
		}

		if (!SceneUtility.CreateObjectResourceLayout(in core, out resLayoutObject))
		{
			Logger.LogError("Failed to create default object resource layout for graphics stack!");
			return false;
		}

		// SHADOW MAPPING:

		if (!shadowMaps.Initialize())
		{
			Logger.LogError("Failed to initialize graphics stack's shadow map module!");
			return false;
		}

		// OUTPUT COMPOSITION:

		if (!Scene.engine.ResourceManager.GetResource(RESOURCE_KEY_FULLSCREEN_QUAD_MESH, out ResourceHandle fullscreenQuadHandle))
		{
			MeshPrimitiveFactory.CreateFullscreenQuadMesh(RESOURCE_KEY_FULLSCREEN_QUAD_MESH, core.graphicsSystem.engine, false, out _, out _, out fullscreenQuadHandle);
		}

		if (!Scene.FindNode(NODE_NAME_COMPOSITE_SCENE_RENDERER, out SceneNode? compositionNode))
		{
			compositionNode = Scene.rootNode.CreateChild(NODE_NAME_COMPOSITE_SCENE_RENDERER);
			compositionNode.LocalTransformation = Pose.Identity;
			if (compositionNode.CreateComponent(out compositeSceneRenderer) && compositeSceneRenderer != null)
			{
				compositeSceneRenderer.SetMaterial(RESOURCE_KEY_COMPOSITE_SCENE_MATERIAL);
				compositeSceneRenderer.SetMesh(fullscreenQuadHandle);
				compositeSceneRenderer.LayerFlags = COMPOSITION_LAYER_MASK;
			}
		}
		if (!Scene.FindNode(NODE_NAME_COMPOSITE_UI_RENDERER, out compositionNode))
		{
			compositionNode = Scene.rootNode.CreateChild(NODE_NAME_COMPOSITE_UI_RENDERER);
			compositionNode.LocalTransformation = Pose.Identity;
			if (compositionNode.CreateComponent(out compositeUIRenderer) && compositeUIRenderer != null)
			{
				compositeUIRenderer.SetMaterial(RESOURCE_KEY_COMPOSITE_UI_MATERIAL);
				compositeUIRenderer.SetMesh(fullscreenQuadHandle);
				compositeUIRenderer.LayerFlags = COMPOSITION_LAYER_MASK;
			}
		}

		Logger.LogMessage($"Initialized graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");

		isDrawing = false;
		isInitialized = true;
		return true;
	}

	public void Shutdown()
	{
		if (!isInitialized) return;

		if (Scene != null)
		{
			if (compositeSceneRenderer != null)
			{
				Scene.rootNode.DestroyChild(compositeSceneRenderer.node);
				compositeSceneRenderer = null;
			}
			if (compositeUIRenderer != null)
			{
				Scene.rootNode.DestroyChild(compositeUIRenderer.node);
				compositeUIRenderer = null;
			}
		}

		sceneObjects.Clear();

		cbScene?.Dispose();
		resLayoutCamera?.Dispose();
		resLayoutObject?.Dispose();
		compositeSceneResourceSet?.Dispose();
		compositeUIResourceSet?.Dispose();

		shadowMaps.Dispose();

		foreach (CommandList cmdList in commandListPool)
		{
			cmdList.Dispose();
		}
		foreach (CommandList cmdList in commandListsInUse)
		{
			cmdList.Dispose();
		}
		commandListPool.Clear();
		commandListsInUse.Clear();

		VisibleRendererCount = 0;
		SkippedRendererCount = 0;
		FailedRendererCount = 0;

		Scene = null;

		isDrawing = false;
		isInitialized = false;

		Logger.LogMessage($"Shut down graphics stack of type '{nameof(ForwardPlusLightsStack)}'.");
	}

	public bool Reset()
	{
		Logger.LogMessage($"Resetting graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene?.Name ?? "NULL"}'.");

		if (IsDisposed)
		{
			Logger.LogError("Cannot reset disposed Forward+Lights graphics stack!");
			return false;
		}

		// Simply shut down the whole stack:
		Shutdown();

		if (!IsValid)
		{
			Logger.LogError("Cannot reinitialize invalid Forward+Lights graphics stack!");
			return false;
		}

		// Then reinitialize it back to its starting state:
		if (!Initialize(Scene!))
		{
			return false;
		}

		bool success = true;

		// If available, refresh post-processing stacks:
		success &= postProcessingStackScene == null || postProcessingStackScene.Refresh();
		success &= postProcessingStackFinal == null || postProcessingStackFinal.Refresh();

		return success;
	}

	internal bool GetOrCreateCommandList(out CommandList _outCmdList)
	{
		if (commandListPool.TryPop(out CommandList? cmdList) && cmdList != null)
		{
			_outCmdList = cmdList;
		}
		else
		{
			_outCmdList = core.MainFactory.CreateCommandList();
		}
		commandListsInUse.Push(_outCmdList);
		return true;
	}

	public bool DrawStack(Scene _scene, List<IRenderer> _renderers, in IList<Camera> _cameras, in IList<ILightSource> _lights)
	{
		if (!IsInitialized)
		{
			Logger.LogError("Cannot draw uninitialized Forward+Lights graphics stack!");
			return false;
		}
		if (_scene != Scene || Scene.IsDisposed)
		{
			Logger.LogError("Cannot draw graphics stack for null or mismatched scene!");
			return false;
		}
		if (_renderers is null)
		{
			Logger.LogError("Cannot draw graphics stack for null list of node-renderers pairs!");
			return false;
		}
		if (_cameras is null)
		{
			Logger.LogError("Cannot draw graphics stack using null camera list!");
			return false;
		}

		// Skip rendering if there are no cameras in the scene:
		if (_cameras.Count == 0)
		{
			VisibleRendererCount = 0;
			SkippedRendererCount = 0;
			isDrawing = false;
			return true;
		}

		bool success = true;
		isDrawing = true;

		uint maxActiveLightCount = Math.Max(core.graphicsSystem.Settings.MaxActiveLightCount, 1);

		// Prepare global resources for drawing the scene and sort out non-visible objects:
		success &= BeginDrawScene(
			in _renderers,
			in _cameras,
			in _lights,
			maxActiveLightCount,
			out SceneContext sceneCtx,
			out Vector3 renderFocalPoint,
			out float renderFocalRadius,
			out bool rebuildResSetCamera,
			out bool texShadowMapsHasChanged);
		if (!success)
		{
			Logger.LogError("Graphics stack failed to begin drawing scene!");
			return false;
		}

		// Skip rendering if no cameras are active:
		if (sceneObjects.activeCameras.Count == 0)
		{
			VisibleRendererCount = 0;
			SkippedRendererCount = 0;
			isDrawing = false;
			return true;
		}

		// Draw shadow maps for all shadow-casting light sources:
		success &= shadowMaps.DrawShadowMaps(
			in sceneCtx,
			renderFocalPoint,
			renderFocalRadius,
			maxActiveLightCount,
			rebuildResSetCamera,
			texShadowMapsHasChanged,
			out uint shadowMapLightCount);

		// Recreate updated scene context if values changed between passes:
		if (shadowMapLightCount != sceneCtx.lightCountShadowMapped)
		{
			sceneCtx = new(sceneCtx.lightCount, shadowMapLightCount)
			{
				Scene = Scene,
				ResLayoutCamera = resLayoutCamera!,
				ResLayoutObject = resLayoutObject!,
				CbScene = cbScene!,
				DummyLightDataBuffer = shadowMaps.dummyLightDataBuffer!,
				ShadowMapArray = shadowMaps.shadowMapArray!,
				SceneResourceVersion = sceneResourceVersion,
			};
		}

		// Draw each active camera component in the scene, and composite output:
		success &= DrawSceneCameras(in sceneCtx, rebuildResSetCamera);

		isDrawing = false;
		return success;
	}

	private bool BeginDrawScene(
		in List<IRenderer> _renderers,
		in IList<Camera> _cameras,
		in IList<ILightSource> _lights,
		uint _maxActiveLightCount,
		out SceneContext _outSceneCtx,
		out Vector3 _outRenderFocalPoint,
		out float _outRenderFocalRadius,
		out bool _outRebuildResSetCamera,
		out bool _outTexShadowsHasChanged)
	{
		// Clear all lists for new frame:
		sceneObjects.Clear();

		// Prepare and sort cameras, light sources, and renderers:
		sceneObjects.PrepareScene(
			in _renderers,
			in _cameras,
			in _lights);

		// Return command lists used in last frame to pool:
		foreach (CommandList cmdList in commandListsInUse)
		{
			commandListPool.Push(cmdList);
		}
		commandListsInUse.Clear();

		// Identify (main) camera focal point:
		{
			Camera focalCamera = Camera.MainCamera != null && Camera.MainCamera.node.scene == Scene
				? Camera.MainCamera
				: _cameras[0];

			float cameraFarClipPlane = focalCamera.ProjectionSettings.farClipPlane;
			Pose cameraWorldPose = focalCamera.node.WorldTransformation;
			_outRenderFocalRadius = LightConstants.directionalLightSize;
			_outRenderFocalPoint = cameraWorldPose.position;
		}

		// Update scene constant buffer:
		if (!CameraUtility.UpdateConstantBuffer_CBScene(
			in core,
			in Scene!.settings,
			ref cbSceneData,
			ref cbScene,
			out _outRebuildResSetCamera))
		{
			Logger.LogError("Failed to create or update scene constant buffer!");
			_outSceneCtx = null!;
			_outTexShadowsHasChanged = false;
			return false;
		}
		if (_outRebuildResSetCamera)
		{
			sceneResourceVersion++;
		}

		// Resize shadow map texture array to reflect maximum number of shadow-casting lights:
		if (!shadowMaps.PrepareShadowMaps(
			_maxActiveLightCount,
			ref _outRebuildResSetCamera,
			ref sceneResourceVersion,
			out _outTexShadowsHasChanged))
		{
			_outSceneCtx = null!;
			return false;
		}

		_outSceneCtx = new(sceneObjects.ActiveLightCount, sceneObjects.ActiveShadowMappedLightsCount)
		{
			Scene = Scene,
			ResLayoutCamera = resLayoutCamera!,
			ResLayoutObject = resLayoutObject!,
			CbScene = cbScene!,
			DummyLightDataBuffer = shadowMaps.dummyLightDataBuffer!,
			ShadowMapArray = shadowMaps.shadowMapArray!,
			SceneResourceVersion = sceneResourceVersion,
		};
		return true;
	}

	private bool DrawSceneCameras(in SceneContext _sceneCtx, bool _rebuildAllResSetCamera)
	{
		bool success = true;

		// Gather light data for each active light source:
		sceneObjects.PrepareLightSourceData();

		// Fetch or create a command list for shadow rendering:
		if (!GetOrCreateCommandList(out CommandList cmdList))
		{
			return false;
		}
		cmdList.Begin();

		List<ILightSource> visibleLights = new((int)sceneObjects.ActiveLightCount);

		for (uint i = 0; i < sceneObjects.ActiveCamerasCount; ++i)
		{
			Camera camera = sceneObjects.activeCameras[(int)i];

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
				Framebuffer sceneFramebuffer = null!;			//TODO [later]: Add all-in-one compositing option, in case no post-processing is needed on scene render.
				if (result)
				{
					result &= CompositeSceneOutput(
						in _sceneCtx,
						camera,
						rebuildResSetCamera,
						i,
						out sceneFramebuffer);
				}

				// Post-processing on scene render:
				if (result && postProcessingStackScene is not null)
				{
					result &= DrawPostProcessingStack(
						in _sceneCtx,
						in cmdList,
						in sceneFramebuffer,
						camera,
						postProcessingStackScene,
						RenderMode.PostProcessing_Scene,
						_rebuildAllResSetCamera,
						i,
						out sceneFramebuffer);
				}

				// UI compositing:
				Framebuffer finalFramebuffer = null!;
				if (result)
				{
					result &= CompositeFinalOutput(
						in _sceneCtx,
						in sceneFramebuffer,
						camera,
						rebuildResSetCamera,
						i,
						out finalFramebuffer);
				}
				
				// Post-processing on final image:
				if (result && postProcessingStackFinal is not null)
				{
					result &= DrawPostProcessingStack(
						in _sceneCtx,
						in cmdList,
						in finalFramebuffer,
						camera,
						postProcessingStackFinal,
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
				Logger.LogException($"An unhandled exception was caught while drawing scene camera {i}!", ex);
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
		Camera _camera,
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
				success &= renderer.Draw(_sceneCtx, cameraPassCtx);			//TODO [important]: Change this to not crash the game if a single renderer fails to draw!
			}
		}

		success &= _camera.EndPass();
		return success;
	}

	private bool DrawPostProcessingStack(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		in Framebuffer _inputFramebuffer,
		Camera _camera,
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
				Logger.LogError("Failed to set override render targets for graphics stack's scene composition pass!");
				_outResultFramebuffer = _inputFramebuffer;
				return false;
			}
		}

		return _postProcessingStack.Draw(in _sceneCtx, in cameraCtx, in _inputFramebuffer, out _outResultFramebuffer);
	}

	private bool CompositeSceneOutput(
		in SceneContext _sceneCtx,
		Camera _camera,
		bool _rebuildResSetCamera,
		uint _cameraIdx,
		out Framebuffer _outSceneFramebuffer)
	{
		if (!GetOrCreateCommandList(out CommandList cmdList))
		{
			_outSceneFramebuffer = null!;
			return false;
		}
		if (compositeSceneRenderer is null || compositeSceneRenderer.IsDisposed)
		{
			_outSceneFramebuffer = null!;
			return false;
		}
		Material compositionMaterial = (compositeSceneRenderer.MaterialHandle!.GetResource(true, true) as Material)!;

		_outSceneFramebuffer = _camera.GetOrCreateCameraTarget(RenderMode.Composition, out CameraTarget target)
			? target.framebuffer
			: null!;

		cmdList.Begin();
		if (!_camera.BeginPass(
			in _sceneCtx,
			cmdList,
			RenderMode.Composition,
			true,
			_cameraIdx,
			0, 0,
			out CameraPassContext cameraPassCtx,
			_rebuildResSetCamera))
		{
			Logger.LogError("Failed to begin frame on graphics stack's composition pass!");
			_camera.SetOverrideCameraTarget(null);
			return false;
		}

		bool success = true;

		// Create resource set containing all render targets that were previously drawn to:
		ResourceLayout resourceLayout = compositionMaterial.BoundResourceLayout!;
		if (resourceLayout is not null && (compositeSceneResourceSet is null || compositeSceneResourceSet.IsDisposed))
		{
			success &= _camera.GetOrCreateCameraTarget(RenderMode.Opaque, out CameraTarget? opaqueTarget);
			success &= _camera.GetOrCreateCameraTarget(RenderMode.Transparent, out CameraTarget? transparentTarget);
			if (!success)
			{
				Logger.LogError("Failed to get camera's targets needed for output composition!");
				return false;
			}

			try
			{
				Texture texNull = core.graphicsSystem.TexPlaceholderTransparent.GetResource<TextureResource>(false, false)!.Texture!;

				BindableResource[] resources =
				[
					opaqueTarget?.texColorTarget ?? texNull,
					opaqueTarget?.texDepthTarget ?? texNull,
					transparentTarget?.texColorTarget ?? texNull,
					transparentTarget?.texDepthTarget ?? texNull,
				];
				ResourceSetDescription resourceSetDesc = new(resourceLayout, resources);

				compositeSceneResourceSet = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
				compositeSceneResourceSet.Name = $"ResSet_Bound_{RESOURCE_KEY_COMPOSITE_SCENE_MATERIAL}";
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create resource set containing render targets for scene composition!", ex, EngineCore.Logging.LogEntrySeverity.Major);
				return false;
			}
		}

		// Bind render targets:
		if (compositeSceneResourceSet is not null && !compositeSceneRenderer.SetOverrideBoundResourceSet(compositeSceneResourceSet))
		{
			Logger.LogError("Failed to override bound resource set for graphics stack's scene composition pass!");
			return false;
		}

		// Send draw calls for output composition:
		success &= compositeSceneRenderer.Draw(_sceneCtx, cameraPassCtx);

		// Finish drawing and submit command list to GPU:
		success &= _camera.EndPass();
		cmdList.End();

		core.CommitCommandList(cmdList);

		// Reset camera state:
		_camera.SetOverrideCameraTarget(null);
		return success;
	}

	private bool CompositeFinalOutput(
		in SceneContext _sceneCtx,
		in Framebuffer _inputFramebuffer,
		Camera _camera,
		bool _rebuildResSetCamera,
		uint _cameraIdx,
		out Framebuffer _outFinalFramebuffer)
	{
		if (!GetOrCreateCommandList(out CommandList cmdList))
		{
			_outFinalFramebuffer = null!;
			return false;
		}
		if (compositeUIRenderer is null || compositeUIRenderer.IsDisposed)
		{
			_outFinalFramebuffer = null!;
			return false;
		}
		Material compositionMaterial = (compositeUIRenderer.MaterialHandle!.GetResource(true, true) as Material)!;

		// If this is the main camera, and there's no post-UI post-processing stack, ouput composited image directly to the swapchain's backbuffer:
		if (_camera.IsMainCamera && postProcessingStackFinal == null)
		{
			Framebuffer backbuffer = core.Device.SwapchainFramebuffer;
			if (!_camera.SetOverrideCameraTarget(backbuffer, false))
			{
				Logger.LogError("Failed to set override render targets for graphics stack's UI composition pass!");
				_outFinalFramebuffer = null!;
				return false;
			}
			_outFinalFramebuffer = backbuffer;
		}
		else if (_camera.GetOrCreateCameraTarget(RenderMode.Composition, out CameraTarget target))
		{
			_outFinalFramebuffer = target.framebuffer;
		}
		else
		{
			Logger.LogError("Failed to retrieve final output framebuffer for graphics stack's UI composition pass!");
			_outFinalFramebuffer = null!;
			return false;
		}

		cmdList.Begin();
		if (!_camera.BeginPass(
			in _sceneCtx,
			cmdList,
			RenderMode.Composition,
			true,
			_cameraIdx,
			0, 0,
			out CameraPassContext cameraPassCtx,
			_rebuildResSetCamera))
		{
			Logger.LogError("Failed to begin frame on graphics stack's composition pass!");
			_camera.SetOverrideCameraTarget(null);
			return false;
		}

		bool success = true;

		// Create resource set containing all render targets that were previously drawn to:
		ResourceLayout resourceLayout = compositionMaterial.BoundResourceLayout!;
		if (resourceLayout is not null && (compositeUIResourceSet is null || compositeUIResourceSet.IsDisposed))
		{
			success &= _camera.GetOrCreateCameraTarget(RenderMode.UI, out CameraTarget? uiTarget);
			if (!success)
			{
				Logger.LogError("Failed to get camera's targets needed for UI output composition!");
				return false;
			}

			try
			{
				Texture texNull = core.graphicsSystem.TexPlaceholderTransparent.GetResource<TextureResource>(false, false)!.Texture!;

				BindableResource[] resources =
				[
					_inputFramebuffer.ColorTargets?[0].Target ?? texNull,
					_inputFramebuffer.DepthTarget?.Target ?? texNull,
					uiTarget?.texColorTarget ?? texNull,
				];
				ResourceSetDescription resourceSetDesc = new(resourceLayout, resources);

				compositeUIResourceSet = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
				compositeUIResourceSet.Name = $"ResSet_Bound_{RESOURCE_KEY_COMPOSITE_UI_MATERIAL}";
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create resource set containing render targets for UI output composition!", ex, EngineCore.Logging.LogEntrySeverity.Major);
				return false;
			}
		}

		// Bind render targets:
		if (compositeUIResourceSet is not null && !compositeUIRenderer.SetOverrideBoundResourceSet(compositeUIResourceSet))
		{
			Logger.LogError("Failed to override bound resource set for graphics stack's UI composition pass!");
			return false;
		}

		// Send draw calls for output composition:
		success &= compositeUIRenderer.Draw(_sceneCtx, cameraPassCtx);

		// Finish drawing and submit command list to GPU:
		success &= _camera.EndPass();
		cmdList.End();

		core.CommitCommandList(cmdList);

		// Reset camera state:
		_camera.SetOverrideCameraTarget(null);
		return success;
	}

	#endregion
}
