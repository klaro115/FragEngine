using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras.Internal;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack.ForwardPlusLights;

internal sealed class ForwardPlusLightsComposition(ForwardPlusLightsStack _stack) : IDisposable
{
	#region Constructors

	~ForwardPlusLightsComposition()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly ForwardPlusLightsStack stack = _stack;
	public readonly GraphicsCore core = _stack.Core;

	private StaticMeshRendererComponent? compositeSceneRenderer = null;
	private StaticMeshRendererComponent? compositeUIRenderer = null;

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

	private Logger Logger => core.graphicsSystem.Engine.Logger ?? Logger.Instance!;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	private void Dispose(bool disposing)
	{
		IsDisposed = true;

		if (disposing)
		{
			compositeSceneRenderer = null;
			compositeUIRenderer = null;
		}
	}

	public bool Initialize()
	{
		if (stack.Scene is null)
		{
			return false;
		}

		bool success = true;

		if (!stack.Scene.engine.ResourceManager.GetResource(RESOURCE_KEY_FULLSCREEN_QUAD_MESH, out ResourceHandle fullscreenQuadHandle))
		{
			success &= MeshPrimitiveFactory.CreateFullscreenQuadMesh(RESOURCE_KEY_FULLSCREEN_QUAD_MESH, core.graphicsSystem.Engine, false, out _, out _, out fullscreenQuadHandle);
		}

		if (!stack.Scene.FindNode(NODE_NAME_COMPOSITE_SCENE_RENDERER, out _))
		{
			SceneNode compositionNode = stack.Scene.rootNode.CreateChild(NODE_NAME_COMPOSITE_SCENE_RENDERER);
			compositionNode.LocalTransformation = Pose.Identity;
			if (compositionNode.CreateComponent(out compositeSceneRenderer) && compositeSceneRenderer != null)
			{
				success &= compositeSceneRenderer.SetMaterial(RESOURCE_KEY_COMPOSITE_SCENE_MATERIAL);
				success &= compositeSceneRenderer.SetMesh(fullscreenQuadHandle);
				compositeSceneRenderer.LayerFlags = COMPOSITION_LAYER_MASK;
			}
		}
		if (!stack.Scene.FindNode(NODE_NAME_COMPOSITE_UI_RENDERER, out _))
		{
			SceneNode compositionNode = stack.Scene.rootNode.CreateChild(NODE_NAME_COMPOSITE_UI_RENDERER);
			compositionNode.LocalTransformation = Pose.Identity;
			if (compositionNode.CreateComponent(out compositeUIRenderer) && compositeUIRenderer != null)
			{
				success &= compositeUIRenderer.SetMaterial(RESOURCE_KEY_COMPOSITE_UI_MATERIAL);
				success &= compositeUIRenderer.SetMesh(fullscreenQuadHandle);
				compositeUIRenderer.LayerFlags = COMPOSITION_LAYER_MASK;
			}
		}

		return success;
	}

	public void Shutdown()
	{
		if (stack.Scene is not null)
		{
			if (compositeSceneRenderer != null)
			{
				stack.Scene.rootNode.DestroyChild(compositeSceneRenderer.node);
				compositeSceneRenderer = null;
			}
			if (compositeUIRenderer != null)
			{
				stack.Scene.rootNode.DestroyChild(compositeUIRenderer.node);
				compositeUIRenderer = null;
			}
		}
	}

	public bool CompositeSceneOutput(
		in SceneContext _sceneCtx,
		CameraComponent _camera,
		bool _rebuildResSetCamera,
		uint _cameraIdx,
		out Framebuffer _outSceneFramebuffer)
	{
		if (!stack.GetOrCreateCommandList(out CommandList cmdList))
		{
			_outSceneFramebuffer = null!;
			return false;
		}
		if (compositeSceneRenderer is null || compositeSceneRenderer.IsDisposed)
		{
			_outSceneFramebuffer = null!;
			return false;
		}
		if (compositeSceneRenderer.MaterialHandle!.GetResource(true, true) is not BasicSurfaceMaterial compositionMaterial)
		{
			_outSceneFramebuffer = null!;
			return false;
		}

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

		// Bind render targets to the composition material as textures:
		{
			success &= _camera.GetOrCreateCameraTarget(RenderMode.Opaque, out CameraTarget? opaqueTarget);
			success &= _camera.GetOrCreateCameraTarget(RenderMode.Transparent, out CameraTarget? transparentTarget);
			if (!success)
			{
				Logger.LogError("Failed to get camera's targets needed for output composition!");
				return false;
			}

			Texture texNull = core.graphicsSystem.TexPlaceholderTransparent.GetResource<TextureResource>(false, false)!.Texture!;

			success &= compositionMaterial.SetResource("TexOpaqueColor", opaqueTarget?.texColorTarget ?? texNull);
			success &= compositionMaterial.SetResource("TexOpaqueDepth", opaqueTarget?.texDepthTarget ?? texNull);
			success &= compositionMaterial.SetResource("TexTransparentColor", transparentTarget?.texColorTarget ?? texNull);
			success &= compositionMaterial.SetResource("TexTransparentDepth", transparentTarget?.texDepthTarget ?? texNull);
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

	public bool CompositeFinalOutput(
		in SceneContext _sceneCtx,
		in Framebuffer _inputFramebuffer,
		CameraComponent _camera,
		bool _rebuildResSetCamera,
		uint _cameraIdx,
		out Framebuffer _outFinalFramebuffer)
	{
		if (!stack.GetOrCreateCommandList(out CommandList cmdList))
		{
			_outFinalFramebuffer = null!;
			return false;
		}
		if (compositeUIRenderer is null || compositeUIRenderer.IsDisposed)
		{
			_outFinalFramebuffer = null!;
			return false;
		}
		if (compositeUIRenderer.MaterialHandle!.GetResource(true, true) is not BasicSurfaceMaterial compositionMaterial)
		{
			_outFinalFramebuffer = null!;
			return false;
		}

		// If this is the main camera, and there's no post-UI post-processing stack, ouput composited image directly to the swapchain's backbuffer:
		if (_camera.IsMainCamera && stack.PostProcessingStackFinal is null)
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

		// Bind render targets to the composition material as textures:
		{
			success &= _camera.GetOrCreateCameraTarget(RenderMode.UI, out CameraTarget? uiTarget);
			if (!success)
			{
				Logger.LogError("Failed to get camera's targets needed for UI output composition!");
				return false;
			}

			Texture texNull = core.graphicsSystem.TexPlaceholderTransparent.GetResource<TextureResource>(false, false)!.Texture!;

			success &= compositionMaterial.SetResource("TexSceneColor", _inputFramebuffer.ColorTargets?[0].Target ?? texNull);
			success &= compositionMaterial.SetResource("TexSceneDepth", _inputFramebuffer.DepthTarget?.Target ?? texNull);
			success &= compositionMaterial.SetResource("TexUIColor", uiTarget?.texColorTarget ?? texNull);
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
