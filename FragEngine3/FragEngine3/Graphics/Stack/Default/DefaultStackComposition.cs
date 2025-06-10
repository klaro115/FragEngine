using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Cameras.Internal;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using System.Numerics;
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

	private CameraInstance? fullscreenCamera = null;
	private CameraPassResources? fullscreenCameraResources = null;		//TODO [later]: replace camera-based render passes by compute shader.

	private ResourceHandle meshFullscreenQuad = ResourceHandle.None;

	private CommandList? cmdListScene = null;
	private CommandList? cmdListUI = null;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized => !IsDisposed && isInitialized;

	#endregion
	#region Constants

	private const string nodeNameCompositionScene = "GraphicsStack_CompositeScene";
	private const string nodeNameCompositionUI = "GraphicsStack_CompositeUI";

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

		cmdListScene?.Dispose();
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
		if (fullscreenCameraResources is not null)
		{
			fullscreenCameraResources.Dispose();
			fullscreenCameraResources = null;
		}
		if (fullscreenCamera is not null)
		{
			fullscreenCamera.Dispose();
			fullscreenCamera = null;
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

		if (!_scene.FindNode(_nodeName, out SceneNode? node) || node is null)
		{
			node = _scene.rootNode.CreateChild(_nodeName);
		}
		node.WorldTransformation = Pose.Identity;

		if (!node!.GetOrCreateComponent(out _renderer) || _renderer is null)
		{
			return false;
		}
		_renderer.LayerFlags = compositionLayer;

		if (resourceManager.IsDisposed || !resourceManager.GetAndLoadResource(_materialName, true, out ResourceHandle materialHandle))
		{
			return false;
		}
		if (!materialHandle.IsLoaded || materialHandle.resourceType != ResourceType.Material)
		{
			return false;
		}

		bool success =
			_renderer.SetMesh(meshFullscreenQuad) &&
			_renderer.SetMaterial(materialHandle);
		return success;
	}

	private bool GetOrCreateFullscreenCamera(in SceneContext _sceneCtx, ref CameraInstance? _camera)	//TODO [later]: Create a re-usable method for all of this in 'CameraUtility' or elsewhere.
	{
		if (_camera is not null && !_camera.IsDisposed)
		{
			return true;
		}

		// Create camera instance:
		CameraSettings settings = new()
		{
			projection = new()
			{
				projectionType = CameraProjectionType.Orthographic,
				nearClipPlane = 0.1f,
				farClipPlane = 1.0f,				 
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

		try
		{
			_camera = new(graphicsCore, false)
			{
				Settings = settings,
				MtxWorld = Matrix4x4.Identity,
			};
		}
		catch (Exception ex)
		{
			logger.LogException("Failed to create camera instance for composition module!", ex);
			return false;
		}

		if (!_camera.GetOrCreateFramebuffer(out _, false))
		{
			return false;
		}

		// Create camera pass resources:
		fullscreenCameraResources?.Dispose();
		fullscreenCameraResources = new();

		// Update CBCamera:
		if (!CameraUtility.UpdateConstantBuffer_CBCamera(
			in _camera,
			Pose.Identity,
			Matrix4x4.Identity,
			Matrix4x4.Identity,
			0,
			0,
			0,
			ref fullscreenCameraResources.cbCameraData,
			ref fullscreenCameraResources.cbCamera!,
			out bool _))
		{
			logger.LogError("Failed to allocate or update camera constant buffer!");
			return false;
		}

		// Update ResSetCamera:
		if (!CameraUtility.UpdateOrCreateCameraResourceSet(
			in graphicsCore,
			in _sceneCtx,
			in fullscreenCameraResources.cbCamera,
			_sceneCtx.DummyLightDataBuffer,
			ref fullscreenCameraResources.resSetCamera,
			out bool _,
			true))
		{
			logger.LogError("Failed to allocate or update camera's default resource set!");
			return false;
		}

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

		success &= _camera.BeginPass(in _sceneCtx, _cmdList, RenderMode.Composition, false, _cameraIdx, _totalLightCount, _totalLightCountShadowMapped, out CameraPassContext cameraPassCtx, _outRebuildResSetCamera);

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

	/*
	public bool CompositeFinalOutput(
		in SceneContext _sceneCtx,
		in IList<CameraComponent> _cameras)
	{
		bool success = true;

		for (int cameraIdx = 0; cameraIdx < _cameras.Count; ++cameraIdx)
		{
			CameraComponent camera = _cameras[cameraIdx];

			success &= CompositeFinalOutput(in _sceneCtx, in camera);
		}

		return success;
	}
	*/

	public bool CompositeFinalOutput(
		in SceneContext _sceneCtx,
		in CameraComponent _camera)
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot composite final output of default graphics stack using uninitialized composition module!");
			return false;
		}

		Framebuffer outputFramebuffer = graphicsCore.Device.SwapchainFramebuffer;

		if (!GetOrCreateFullscreenCamera(in _sceneCtx, ref fullscreenCamera))
		{
			logger.LogError("Failed to create camera instance for final output composition!");
			return false;
		}

		if (!fullscreenCamera!.SetOverrideFramebuffer(outputFramebuffer, true))
		{
			logger.LogError("Failed to set output frame buffer as camera's override render target!");
			return false;
		}

		if (!fullscreenCamera!.GetOrCreateFramebuffer(out Framebuffer framebufferSceneComposition) ||
			!_camera.GetOrCreateCameraTarget(RenderMode.UI, out CameraTarget targetUI))
		{
			logger.LogError("Cannot composite final output of default graphics stack; render targets missing for scene composition or UI pass!");
			return false;
		}

		if (cmdListUI is null && !graphicsCore.CreateCommandList(out cmdListUI))
		{
			logger.LogError("Cannot composite final output of default graphics stack without command list!");
			return false;
		}
		cmdListUI!.Begin();

		bool success = true;

		success &= BeginCameraPass(in _sceneCtx, cmdListUI, outputFramebuffer, _camera.FrameCounter, _camera.PassCounter + 2, out CameraPassContext? cameraPassCtx);

		Material material = rendererUI!.MaterialHandle.GetResource<Material>(true, true)!;
		success &= material.SetResource("TexSceneColor", framebufferSceneComposition.ColorTargets[0].Target);   //TODO [later]: Query slot indices by name during initialization, then use those at run-time.
		success &= material.SetResource("TexSceneDepth", framebufferSceneComposition.DepthTarget!.Value.Target);
		success &= material.SetResource("TexUIColor", targetUI.texColorTarget);

		if (success)
		{
			success &= rendererUI!.Draw(_sceneCtx, cameraPassCtx!);
		}

		success &= fullscreenCamera.EndDrawing();

		cmdListUI!.End();
		if (success)
		{
			success = graphicsCore.CommitCommandList(cmdListUI);
		}

		return success;
	}

	private bool BeginCameraPass(in SceneContext _sceneCtx, CommandList _cmdList, Framebuffer _framebuffer, uint _frameIdx, uint _passIdx, out CameraPassContext? _outCameraPassCtx)
	{
		if (!fullscreenCamera!.BeginDrawing(_cmdList, true, true, out _))
		{
			logger.LogError("Failed to begin drawing composition pass!");
			_outCameraPassCtx = null;
			return false;
		}

		_outCameraPassCtx = new()
		{
			CameraInstance = fullscreenCamera,
			CmdList = _cmdList,
			Framebuffer = _framebuffer,
			ResSetCamera = fullscreenCameraResources!.resSetCamera!,
			CbCamera = fullscreenCameraResources.cbCamera!,
			LightDataBuffer = _sceneCtx.DummyLightDataBuffer,
			CameraResourceVersion = 0,
			FrameIdx = _frameIdx,
			PassIdx = _passIdx,
			LightCountShadowMapped = 0,
			MtxWorld2Clip = Matrix4x4.Identity,
			OutputDesc = _framebuffer.OutputDescription,
			MirrorY = fullscreenCamera.ProjectionSettings.mirrorY,
		};
		return true;
	}

	#endregion
}
