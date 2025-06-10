using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Cameras.Internal;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackComposition(GraphicsCore _graphicsCore) : IDisposable
{
	#region Constructors

	~DefaultStackComposition()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore;
	private readonly ResourceManager resourceManager = _graphicsCore.graphicsSystem.Engine.ResourceManager;
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

	private bool isInitialized = false;

	private StaticMeshRendererComponent? rendererScene = null;
	private StaticMeshRendererComponent? rendererUI = null;

	private CameraComponent? finalOutputCamera = null;

	private ResourceHandle meshFullscreenQuad = ResourceHandle.None;

	private CommandList? cmdListUI = null;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized => !IsDisposed && isInitialized;

	#endregion
	#region Constants

	private const string nodeNameCompositionScene = "GraphicsStack_CompositeScene";
	private const string nodeNameCompositionUI = "GraphicsStack_CompositeUI";
	private const string nodeNameFinalOutputCamera = "GraphicsStack_FinalOutputCamera";

	private const string materialNameCompositeScene = "Mtl_ForwardPlusLight_CompositeScene";
	private const string materialNameCompositeUI = "Mtl_ForwardPlusLight_CompositeUI";

	private const string meshNameFullscreenQuad = "FullscreenQuad";

	private const uint compositionLayer = 0x800000u;

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
		if (isInitialized)
		{
			Shutdown();
		}

		cmdListUI?.Dispose();
	}

	public bool Initialize(Scene _scene)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot initialize composition for default graphics that has already been disposed!");
			return false;
		}
		if (IsInitialized)
		{
			logger.LogError("Cannot re-initialize composition for default graphics; module is already initialized!");
			return false;
		}
		if (_scene is null || _scene.IsDisposed || _scene.rootNode.IsDisposed)
		{
			logger.LogError("Cannot initialize composition for default graphics stack of null or disposed scene!");
			return false;
		}

		if (!GetOrCreateFullScreenQuad())
		{
			logger.LogError("Failed to initialize fullscreen quad mesh for composition of default graphics stack; !");
			return false;
		}

		if (!GetOrCreateFullscreenRenderer(_scene, nodeNameCompositionScene, materialNameCompositeScene, ref rendererScene))
		{
			logger.LogError("Failed to initialize renderer for scene composition of default graphics stack; !");
			return false;
		}
		if (!GetOrCreateFullscreenRenderer(_scene, nodeNameCompositionUI, materialNameCompositeUI, ref rendererUI))
		{
			logger.LogError("Failed to initialize renderer for UI composition of default graphics stack; !");
			return false;
		}

		if (!GetOrCreateFinalOutputCamera(_scene))
		{
			logger.LogError("Failed to initialize camera for final output composition of default graphics stack; !");
			return false;
		}

		//TODO [later]: Register listeners for lifecycle events of renderer components

		isInitialized = true;
		return true;
	}

	public void Shutdown()
	{
		isInitialized = false;

		if (rendererScene is not null)
		{
			rendererScene.node.DestroyNode();
			rendererScene = null;
		}
		if (rendererUI is not null)
		{
			rendererUI.node.DestroyNode();
			rendererUI = null;
		}
		if (finalOutputCamera is not null)
		{
			finalOutputCamera?.node.DestroyNode();
			finalOutputCamera = null;
		}
		//...
	}

	private bool GetOrCreateFullScreenQuad()
	{
		if (resourceManager.GetResource(meshNameFullscreenQuad, out meshFullscreenQuad))
		{
			return true;
		}

		bool success = MeshPrimitiveFactory.CreateFullscreenQuadMesh(meshNameFullscreenQuad, graphicsCore.graphicsSystem.Engine, false, out _, out _, out meshFullscreenQuad);
		return success;
	}

	private bool GetOrCreateFullscreenRenderer(Scene _scene, string _nodeName, string _materialName, ref StaticMeshRendererComponent? _renderer)
	{
		if (_renderer is not null && !_renderer.IsDisposed)
		{
			_renderer.LayerFlags = compositionLayer;
			return true;
		}

		// Get or create renderer node and component:
		if (!_scene.FindNode(_nodeName, out SceneNode? node) || node is null)
		{
			node = _scene.rootNode.CreateChild(_nodeName);
		}
		node.WorldTransformation = Pose.Identity;

		if (!node.GetOrCreateComponent(out _renderer) || _renderer is null)
		{
			return false;
		}
		_renderer.LayerFlags = compositionLayer;

		// Load composition material:
		if (resourceManager.IsDisposed || !resourceManager.GetAndLoadResource(_materialName, true, out ResourceHandle materialHandle))
		{
			return false;
		}
		if (!materialHandle.IsLoaded || materialHandle.resourceType != ResourceType.Material)
		{
			return false;
		}

		// Configure renderer component:
		bool success =
			_renderer.SetMesh(meshFullscreenQuad) &&
			_renderer.SetMaterial(materialHandle);
		return success;
	}
	
	private bool GetOrCreateFinalOutputCamera(Scene _scene)
	{
		if (finalOutputCamera is not null && !finalOutputCamera.IsDisposed)
		{
			finalOutputCamera.layerMask = compositionLayer;
			finalOutputCamera.node.IsEnabled = false;
			finalOutputCamera.SetOverrideCameraTarget(null);
			finalOutputCamera.MarkDirty();
			return true;
		}

		// Get or create camera node and component:
		if (!_scene.FindNode(nodeNameFinalOutputCamera, out SceneNode? node) || node is null)
		{
			node = _scene.rootNode.CreateChild(nodeNameFinalOutputCamera);
		}
		node.WorldTransformation = Pose.Identity;

		if (!node.GetOrCreateComponent(out finalOutputCamera) || finalOutputCamera is null)
		{
			return false;
		}

		// Configure camera component:
		finalOutputCamera.Settings = new CameraSettings()
		{
			projection = new()
			{
				projectionType = CameraProjectionType.Orthographic,
				nearClipPlane = 0.1f,
				farClipPlane = 10.0f,
			},
			output = new()
			{
				colorFormat = graphicsCore.DefaultColorTargetPixelFormat,
				depthFormat = graphicsCore.DefaultDepthTargetPixelFormat,
				hasDepth = true,
				hasStencil = false,
				resolutionX = (uint)graphicsCore.Window.Width,
				resolutionY = (uint)graphicsCore.Window.Height,
			},
			clearing = new()
			{
				clearColor = true,
				clearDepth = true,
				clearStencil = false,
				clearColorValue = new RgbaFloat(0, 0, 0, 0),
				clearDepthValue = 1.0f,
				clearStencilValue = 0,
			},
		};
		finalOutputCamera.layerMask = compositionLayer;
		finalOutputCamera.node.IsEnabled = false;
		finalOutputCamera.SetOverrideCameraTarget(null);
		finalOutputCamera.MarkDirty();
		return true;
	}

	public bool CompositeSceneOutput(
		in SceneContext _sceneCtx,
		in CameraComponent _camera,
		uint _cameraIdx,
		CommandList _cmdList,
		uint _totalLightCount,
		uint _totalLightCountShadowMapped,
		ref bool _outRebuildResSetCamera)
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot composite scene output of default graphics stack using uninitialized composition module!");
			return false;
		}

		if (!_camera.GetOrCreateCameraTarget(RenderMode.Opaque, out CameraTarget targetOpaque) ||
			!_camera.GetOrCreateCameraTarget(RenderMode.Transparent, out CameraTarget targetTransparent))
		{
			logger.LogError("Cannot composite scene output of default graphics stack; render targets missing for opaque or transparent pass!");
			return false;
		}

		bool success = true;

		//TEST
		//_camera.SetOverrideCameraTarget(graphicsCore.Device.SwapchainFramebuffer);

		success &= _camera.BeginPass(in _sceneCtx, _cmdList, RenderMode.Composition, true, _cameraIdx, _totalLightCount, _totalLightCountShadowMapped, out CameraPassContext cameraPassCtx, _outRebuildResSetCamera);

		Material material = rendererScene!.MaterialHandle.GetResource<Material>(true, true)!;
		success &= material.SetResource("TexOpaqueColor", targetOpaque.texColorTarget);
		success &= material.SetResource("TexOpaqueDepth", targetOpaque.texDepthTarget);
		success &= material.SetResource("TexTransparentColor", targetTransparent.texColorTarget);
		success &= material.SetResource("TexTransparentDepth", targetTransparent.texDepthTarget);   //TODO [later]: Query slot indices by name during initialization, then use those at run-time.

		if (success)
		{
			success &= rendererScene!.Draw(_sceneCtx, cameraPassCtx!);
		}

		success &= _camera.EndPass();

		return success;
	}

	public bool CompositeFinalOutput(
		in SceneContext _sceneCtx,
		in CameraComponent _mainCamera)
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot composite final output of default graphics stack using uninitialized composition module!");
			return false;
		}

		// (Re)activate and prepare camera:
		finalOutputCamera!.node.SetEnabled(true);

		Framebuffer outputFramebuffer = graphicsCore.Device.SwapchainFramebuffer;
		if (!finalOutputCamera.SetOverrideCameraTarget(outputFramebuffer, false))
		{
			logger.LogError("Failed to set output frame buffer as camera's override render target!");
			return false;
		}

		// Get main camera's fully composited scene render:
		if (!_mainCamera.GetOrCreateCameraTarget(RenderMode.Composition, out CameraTarget targetComposition) ||			//TODO [later]: Change this to use post-processing output instead, if available and once implemented.
			!_mainCamera.GetOrCreateCameraTarget(RenderMode.UI, out CameraTarget targetUI))
		{
			logger.LogError("Cannot composite final output of default graphics stack; render targets missing for scene composition or UI pass!");
			return false;
		}

		// Begin drawing output frame:
		if (!finalOutputCamera.BeginFrame(0, out bool rebuildResSetCamera))
		{
			logger.LogError("Failed to begin compositing final output frame!");
			return false;
		}

		if (cmdListUI is null && !graphicsCore.CreateCommandList(out cmdListUI))
		{
			logger.LogError("Cannot composite final output of default graphics stack without command list!");
			return false;
		}
		cmdListUI!.Begin();

		//cmdListUI.SetFramebuffer(outputFramebuffer);
		//cmdListUI.ClearColorTarget(0, RgbaFloat.Red);
		//cmdListUI.ClearDepthStencil(1.0f, 0);

		//cmdListUI.SetFramebuffer(targetUI.framebuffer);
		//cmdListUI.ClearColorTarget(0, new RgbaFloat(0, 0, 0, 0));
		//cmdListUI.ClearDepthStencil(1.0f);

		bool success = true;

		success &= finalOutputCamera.BeginPass(in _sceneCtx, cmdListUI, RenderMode.Composition, true, 100, 0, 0, out CameraPassContext cameraPassCtx, rebuildResSetCamera);

		Material material = rendererUI!.MaterialHandle.GetResource<Material>(true, true)!;
		success &= material.SetResource("TexSceneColor", targetComposition.texColorTarget);   //TODO [later]: Query slot indices by name during initialization, then use those at run-time.
		success &= material.SetResource("TexSceneDepth", targetComposition.texDepthTarget);
		success &= material.SetResource("TexUIColor", targetUI.texColorTarget);

		if (success)
		{
			success &= rendererUI!.Draw(_sceneCtx, cameraPassCtx!);
		}

		success &= finalOutputCamera.EndPass();

		// End output frame:
		cmdListUI!.End();
		if (success)
		{
			success = graphicsCore.CommitCommandList(cmdListUI);
		}

		// Deactivate output camera again, so it won't be detected by other stack modules:
		finalOutputCamera.node.SetEnabled(false);
		return success;
	}

	#endregion
}
