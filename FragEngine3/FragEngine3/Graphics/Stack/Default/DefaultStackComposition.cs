using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;
using FragEngine3.Scenes;

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

	private ResourceHandle meshFullscreenQuad = ResourceHandle.None;

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

		//TODO: Register listeners for lifecycle events of renderer components

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

		if (!_scene.FindNode(nodeNameCompositionScene, out SceneNode? node) || node is null)
		{
			node = _scene.rootNode.CreateChild(nodeNameCompositionScene);
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

	public bool CompositeOutput()
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot composite rendering output of default graphics stack using uninitialized composition module!");
			return false;
		}



		//TODO

		return true;    //TEMP
	}

	#endregion
}
