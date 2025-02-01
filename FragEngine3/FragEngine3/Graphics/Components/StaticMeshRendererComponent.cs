using System.Numerics;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Renderers;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components;

[ComponentBackingType(typeof(StaticMeshRenderer))]
public sealed class StaticMeshRendererComponent : Component, IPhysicalRenderer
{
	#region Constructors

	public StaticMeshRendererComponent(SceneNode _node) : base(_node)
	{
		graphicsCore = _node?.scene.engine.GraphicsSystem.graphicsCore ??  throw new ArgumentNullException(nameof(_node), "Node and graphics core may not be null!");

		instance = new(graphicsCore, node.Name);
		instance.OnResourcesChanged += OnInstanceResourcesChangedListener;
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever the renderer's resources (mesh and materials) have been changed.
	/// </summary>
	public event Action<StaticMeshRendererComponent>? OnResourcesChanged = null;

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;

	private readonly StaticMeshRenderer instance;

	#endregion
	#region Properties

	public GraphicsCore GraphicsCore => graphicsCore;

	public ResourceHandle MeshHandle => instance.MeshHandle;
	public ResourceHandle MaterialHandle => instance.MaterialHandle;
	public ResourceHandle ShadowMaterialHandle => instance.ShadowMaterialHandle;

	public bool AreResourcesAssigned => instance.AreResourcesAssigned;
	public bool AreShadowResourcesAssigned => instance.AreShadowResourcesAssigned;
	public bool IsVisible => !IsDisposed && instance.IsVisible && node.IsEnabled;
	public bool DontDrawUnlessFullyLoaded
	{
		get => instance.DontDrawUnlessFullyLoaded;
		set => instance.DontDrawUnlessFullyLoaded = value;
	}

	public RenderMode RenderMode => instance.RenderMode;
	public uint LayerFlags
	{
		get => instance.LayerFlags;
		set => instance.LayerFlags = value;
	}

	public Vector3 VisualCenterPoint => instance.VisualCenterPoint;

	public float BoundingRadius => instance.BoundingRadius;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		instance.Dispose();
	}

	/// <summary>
	/// Manually flag the renderer as dirty, forcing a rebuild of the pipeline and constant buffers before the next draw call.
	/// </summary>
	public void MarkDirty() => instance.MarkDirty();

	private void OnInstanceResourcesChangedListener(StaticMeshRenderer instance)
	{
		if (!IsDisposed)
		{
			OnResourcesChanged?.Invoke(this);
		}
	}

	public bool SetMesh(string _resourceKey) => !IsDisposed && instance.SetMesh(_resourceKey);

	/// <summary>
	/// Assigns a mesh that shall be drawn by this renderer.
	/// </summary>
	/// <param name="_meshHandle">A resource handle for the mesh. If null or invalid, the mesh will be unassigned.</param>
	/// <returns>True if the mesh was assigned, false otherwise.</returns>
	public bool SetMesh(ResourceHandle? _meshHandle) => !IsDisposed && instance.SetMesh(_meshHandle);

	public bool SetMaterial(string _resourceKey) => !IsDisposed && instance.SetMaterial(_resourceKey);

	/// <summary>
	/// Assigns a material for rendering the mesh.
	/// </summary>
	/// <param name="_materialHandle">A resource handle for the material. If null or invalid, the material will be unassigned.</param>
	/// <param name="_overrideShadowMaterial">An override material for rendering shadow maps. If non-null, this material replaces any
	/// shadow material provided by the main material.</param>
	/// <returns>True if the material was assigned, false otherwise.</returns>
	public bool SetMaterial(ResourceHandle? _materialHandle, ResourceHandle? _overrideShadowMaterial = null) => !IsDisposed && instance.SetMaterial(_materialHandle, _overrideShadowMaterial);

	public bool SetOverrideBoundResourceSet(ResourceSet? _newOverrideResourceSet) => !IsDisposed && instance.SetOverrideBoundResourceSet(_newOverrideResourceSet);

	public float GetZSortingDepth(Vector3 _viewportPosition, Vector3 _cameraDirection) => instance.GetZSortingDepth(_viewportPosition, _cameraDirection);

	public bool Draw(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx)
	{
		if (!IsVisible) return true;

		if (instance.LastUpdatedForFrameIdx != _cameraPassCtx.FrameIdx)
		{
			instance.SetWorldPose(node.WorldTransformation);
		}

		return instance.Draw(_sceneCtx, _cameraPassCtx);
	}
	public bool DrawShadowMap(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx)
	{
		if (!IsVisible) return true;

		if (instance.LastUpdatedForFrameIdx != _cameraPassCtx.FrameIdx)
		{
			instance.SetWorldPose(node.WorldTransformation);
		}

		return instance.DrawShadowMap(_sceneCtx, _cameraPassCtx);
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		if (IsDisposed || instance.IsDisposed)
		{
			Logger.LogError("Cannot load data on disposed static mesh renderer!");
			return false;
		}
		if (string.IsNullOrEmpty(_componentData.SerializedData))
		{
			Logger.LogError("Cannot load static mesh renderer from null or blank serialized data!");
			return false;
		}

		// Deserialize renderer data from component data's serialized data string:
		if (!Serializer.DeserializeFromJson(_componentData.SerializedData, out StaticMeshRendererData? data) || data == null)
		{
			Logger.LogError("Failed to deserialize static mesh renderer component data from JSON!");
			return false;
		}

		ResourceManager resourceManager = graphicsCore.graphicsSystem.Engine.ResourceManager;

		// Reset all resource references:
		instance.SetMesh(ResourceHandle.None);
		instance.SetMaterial(ResourceHandle.None, ResourceHandle.None);

		DontDrawUnlessFullyLoaded = data.DontDrawUnlessFullyLoaded;
		LayerFlags = data.LayerFlags;

		bool success = true;

		// Load resource handles and queue up loading if they're not available yet:
		if (!string.IsNullOrEmpty(data.Material))
		{
			ResourceHandle? handleShadows = null;

			if (!resourceManager.GetResource(data.Material, out ResourceHandle handle) || handle.resourceType != ResourceType.Material)
			{
				Logger.LogError($"A material resource with the key '{data.Material}' could not be found!");
				return false;
			}
			if (!string.IsNullOrEmpty(data.ShadowMaterial) && !resourceManager.GetResource(data.ShadowMaterial, out handleShadows) && handleShadows.resourceType == ResourceType.Material)
			{
				Logger.LogError($"A shadow material resource with the key '{data.ShadowMaterial}' could not be found!");
				return false;
			}

			success &= SetMaterial(handle, handleShadows);
		}
		if (!string.IsNullOrEmpty(data.Mesh))
		{
			if (!resourceManager.GetResource(data.Mesh, out ResourceHandle handle) || handle.resourceType != ResourceType.Model)
			{
				Logger.LogError($"A static mesh resource with the key '{data.Mesh}' could not be found!");
				return false;
			}

			success &= SetMesh(handle);
		}
		return success;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		// Gather resource keys:
		string keyMesh = MeshHandle?.resourceKey ?? string.Empty;
		string keyMaterialScene = MaterialHandle?.resourceKey ?? string.Empty;
		string keyMaterialShadow = string.Empty;

		if (ShadowMaterialHandle is not null && ShadowMaterialHandle.IsValid)
		{
			keyMaterialShadow = ShadowMaterialHandle.resourceKey;
		}
		else if (MaterialHandle is not null && MaterialHandle.IsValid)
		{
			Material? materialScene = MaterialHandle.GetResource<Material>(false, false);
			if (materialScene is not null)
			{
				keyMaterialShadow = materialScene.resourceKey;
			}
		}

		// Assemble serializable data:
		StaticMeshRendererData data = new()
		{
			Mesh = keyMesh,
			Material = keyMaterialScene,
			ShadowMaterial = keyMaterialShadow,

			DontDrawUnlessFullyLoaded = DontDrawUnlessFullyLoaded,
			LayerFlags = LayerFlags,
		};

		if (!Serializer.SerializeToJson(data, out string dataJson))
		{
			Logger.LogError("Failed to serialize static mesh renderer component data to JSON!");
			_componentData = ComponentData.Empty;
			return false;
		}

		_componentData = new ComponentData()
		{
			SerializedData = dataJson,
		};
		return true;
	}

	public override string ToString()
	{
		return $"StaticMeshRenderer, Node: {node.Name}, Mesh: '{MeshHandle.resourceKey}', Material: '{MaterialHandle.resourceKey}', Shadow: '{ShadowMaterialHandle?.resourceKey ?? "NULL"}', RenderMode: '{RenderMode}', IsVisible: {IsVisible}";
	}

	#endregion
}
