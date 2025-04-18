using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.MaterialTypes;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

/// <summary>
/// Base class for materials. Materials are graphics resources that define a combination shaders, textures,
/// and constant buffers. They are used to create and manage resource sets which can be bounds to a graphics
/// pipeline by an implementation of <see cref="IRenderer"/>. The renderer creates the <see cref="Pipeline"/>,
/// but the material provides the resources and bindings for it.
/// </summary>
public abstract class Material : Resource
{
	#region Constructors

	protected Material(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_resourceHandle)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
		logger = graphicsCore.graphicsSystem.Engine.Logger;
		materialType = _data.MaterialType;
		renderMode = _data.RenderMode;
		//maxSupportedVariantFlags = _data. ???			//TODO: Initialize this value and add appropriate fields or getter methods to MaterialData!

		// Assign replacement shaders:
		if (_data.Replacements is not null)
		{
			if (!string.IsNullOrEmpty(_data.Replacements.ShadowMap) && resourceManager.GetResource(_data.Replacements.ShadowMap, out ResourceHandle shadowMaterialHandle))
			{
				ShadowMaterialHandle = shadowMaterialHandle;
			}
			if (!string.IsNullOrEmpty(_data.Replacements.ShadowMap) && resourceManager.GetResource(_data.Replacements.ShadowMap, out ResourceHandle simplifiedMaterialHandle))
			{
				SimplifiedMaterialHandle = simplifiedMaterialHandle;
			}
		}

		// Prepare custom constant buffers:
		if (!CreateCustomConstantBufferSlots(_data, out customConstantBufferSlots))
		{
			Dispose();
			return;
		}
	}

	~Material()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered when the material's bound resources have changed.
	/// </summary>
	public event OnMaterialBoundResourcesChangedHandler? BoundResourcesChanged = null;

	/// <summary>
	/// Event that is triggered when the material's replacement materials (shadow or simplified) have changed.
	/// </summary>
	public event OnMaterialReplacementsChangedHandler? ReplacementsChanged = null;

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;
	protected readonly Logger logger;

	public readonly MaterialType materialType;
	public readonly RenderMode renderMode;
	public readonly MeshVertexDataFlags maxSupportedVariantFlags;

	protected ConstantBufferSlot[] customConstantBufferSlots;

	#endregion
	#region Properties

	public override ResourceType ResourceType => ResourceType.Material;

	/// <summary>
	/// Gets a resource handle for the replacement material that's used to render shadow maps for this material.
	/// </summary>
	public ResourceHandle ShadowMaterialHandle { get; private set; } = ResourceHandle.None;
	/// <summary>
	/// Gets the replacement material that's used to render shadow maps for this material.
	/// </summary>
	public Material? ShadowMaterial { get; private set; } = null;
	/// <summary>
	/// Gets whether this material has a replacement material for shadow maps assigned.
	/// </summary>
	public bool HasShadowMapMaterialVersion => ShadowMaterial is not null || ShadowMaterialHandle.IsValid;

	/// <summary>
	/// Gets a resource handle for the replacement material that's used to render simplified versions or distant LODs of this material.
	/// </summary>
	public ResourceHandle SimplifiedMaterialHandle{ get; private set; } = ResourceHandle.None;
	/// <summary>
	/// Gets the replacement material that's used to render simplified versions or distant LODs of this material.
	/// </summary>
	public Material? SimplifiedMaterial { get; private set; } = null;
	/// <summary>
	/// Gets whether this material has a replacement material for simplified rendering assigned.
	/// </summary>
	public bool HasSimplifiedMaterialVersion => ShadowMaterial is not null || ShadowMaterialHandle.IsValid;

	public float ZSortingBias { get; set; } = 0.0f;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		foreach (var slot in customConstantBufferSlots)
		{
			slot.Dispose();
		}
	}

	/// <summary>
	/// Creates a new graphics pipeline for a specific renderer's use-case.
	/// </summary>
	/// <param name="_sceneCtx">A context object with rendering data for the current scene.</param>
	/// <param name="_cameraCtx">A context object with rendering data for the current camera pass.</param>
	/// <param name="_vertexDataFlags">Bit flags of all vertex data flags that are available and used by the renderer.
	/// For standard surface materials, at least the '<see cref="MeshVertexDataFlags.BasicSurfaceData"/>' flag must be raised.
	/// For compute shader materials, this value is ignored.</param>
	/// <param name="_outPipelineState">Outputs a new pipeline state object for the specified vertex data configuration, or null on failure.</param>
	/// <param name="_outIsFullyLoaded">Outputs whether the pipeline is ready for drawing yet.
	/// If false, some underlying resources are still being loaded and drawing of the material should be skipped.</param>
	/// <returns>False if pipeline creation failed, true otherwise.
	/// This will return true both if the material is not fully loaded yet and no pipeline created, and alos when created successfully.</returns>
	public abstract bool CreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, MeshVertexDataFlags _vertexDataFlags, out PipelineState? _outPipelineState, out bool _outIsFullyLoaded);

	/// <summary>
	/// Prepares that material's resources for rendering and binds resources and shaders to the graphics pipeline.
	/// </summary>
	/// <param name="_sceneCtx">A context object with rendering data for the current scene.</param>
	/// <param name="_cameraPassCtx">A context object with rendering data for the current camera pass.</param>
	///	<param name="_resSetObject">The renderer's object/mesh resource set. For non-physical renderers or compute materials, this may be null.</param>
	/// <param name="_resourceSets">Reference to an array of all resource sets used by this material. If null, a new one will be created.</param>
	/// <returns>True if the material could be prepared, and resources are loaded for imminent rendering, false otherwise.</returns>
	public abstract bool Prepare(in SceneContext _sceneCtx, in CameraPassContext _cameraPassCtx, ResourceSet? _resSetObject, ref ResourceSet[]? _resourceSets);

	#endregion
	#region Methods Common

	/// <summary>
	/// Tries to create a new resource set for user-bound resources (i.e. resources not managed by the engine).
	/// </summary>
	/// <param name="_resourceLayout">A resource layout describing the layout and types of bound resources.</param>
	/// <param name="_outResourceSet">Outputs a new resource set matching the given layout, and containing the given resources. Null on failure.</param>
	/// <param name="_boundResources">An array of resources that shall be bound using this resource set.</param>
	/// <returns>True if a new resource set was created successfully, false otherwise.</returns>
	protected bool CreateResourceSetForBoundResources(ResourceLayout _resourceLayout, out ResourceSet? _outResourceSet, params BindableResource[] _boundResources)
	{
		if (_resourceLayout is null || _resourceLayout.IsDisposed)
		{
			logger.LogError($"Cannot create resource set for bound resources using null or disposed layout! (Resource key: {resourceKey})");
			_outResourceSet = null;
			return false;
		}
		if (_boundResources is null)// || _boundResources.Length == 0)
		{
			logger.LogError($"Cannot populate resource set for bound resources using null or empty resources array! (Resource key: {resourceKey})");
			_outResourceSet = null;
			return false;
		}

		try
		{
			ResourceSetDescription resSetDesc = new(_resourceLayout, _boundResources);

			_outResourceSet = graphicsCore.MainFactory.CreateResourceSet(ref resSetDesc);
			_outResourceSet.Name = $"ResSetBound_{resourceKey}";
			return true;
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create resource set for bound resources! (Resource key: {resourceKey})", ex);
			_outResourceSet = null;
			return false;
		}
	}

	protected bool CreateCustomConstantBufferSlots(MaterialDataNew _data, out ConstantBufferSlot[] _outSlots)
	{
		if (_data.Constants is null || _data.Constants.Length == 0)
		{
			_outSlots = [];
			return true;
		}

		int customCbCount = _data.Constants.Count(o => o.Type == ConstantBuffers.ConstantBufferType.Custom);
		_outSlots = new ConstantBufferSlot[customCbCount];

		int customSlotIdx = 0;
		for (int i = 0; i < _data.Constants.Length; i++)
		{
			MaterialConstantBufferData cbData = _data.Constants[i];
			if (cbData.Type != ConstantBuffers.ConstantBufferType.Custom)
			{
				continue;
			}

			if (!ConstantBufferSlot.CreateSlot(graphicsCore, cbData, out ConstantBufferSlot slot))
			{
				logger.LogError($"Failed to create custom constant buffer slot for CB {i} of material '{resourceKey}'!");
				return false;
			}

			_outSlots[customSlotIdx++] = slot;
		}
		return true;
	}

	#endregion
	#region Methods Replacements

	/// <summary>
	/// Assigns a replacement material that'll be used to draw shadow maps for this material.
	/// </summary>
	/// <param name="_resourceKey">A resource key identifying the material resource.</param>
	/// <param name="_loadImmediately">Whether to block and load the new material immediately, if it isn't loaded yet.
	/// If false, the material is queued up for asynchronous loading instead.</param>
	/// <returns>True if the new replacement material could be loaded and assigned, false otherwise.</returns>
	public bool SetShadowMaterial(string _resourceKey, bool _loadImmediately = false)
	{
		if (string.IsNullOrEmpty(_resourceKey) || !resourceManager.GetResource(_resourceKey, out ResourceHandle handle))
		{
			handle = ResourceHandle.None;
		}
		return SetShadowMaterial(handle, _loadImmediately);
	}

	/// <summary>
	/// Assigns a replacement material that'll be used to draw shadow maps for this material.
	/// </summary>
	/// <param name="_handle">The resource handle that manages the material resource.</param>
	/// <param name="_loadImmediately">Whether to block and load the new material immediately, if it isn't loaded yet.
	/// If false, the material is queued up for asynchronous loading instead.</param>
	/// <returns>True if the new replacement material could be loaded and assigned, false otherwise.</returns>
	public bool SetShadowMaterial(ResourceHandle _handle, bool _loadImmediately = false)
	{
		bool hasChanged = CompareKeys(ShadowMaterialHandle, _handle);

		// Null or invalid handles will unassign the shadow material:
		if (_handle is null || !_handle.IsValid)
		{
			ShadowMaterialHandle = ResourceHandle.None;
			ShadowMaterial = null;

			if (hasChanged)
			{
				ReplacementsChanged?.Invoke(this);
			}
			return true;
		}

		ShadowMaterialHandle = _handle;
		ShadowMaterial = ShadowMaterialHandle.GetResource<Material>(_loadImmediately);

		// Unassign shadow material if loading has failed:
		if (_loadImmediately && ShadowMaterial is null)
		{
			logger.LogError($"Failed to load shadow material replacement '{_handle}'!");
			ShadowMaterialHandle = ResourceHandle.None;
			return false;
		}

		if (hasChanged)
		{
			ReplacementsChanged?.Invoke(this);
		}
		return true;
	}

	/// <summary>
	/// Assigns a replacement material that'll be used to draw shadow maps for this material.
	/// </summary>
	/// <param name="_material">The replacement material.</param>
	/// <returns>True if the new replacement material could be assigned, false otherwise.</returns>
	public bool SetShadowMaterial(Material? _material)
	{
		bool hasChanged = CompareKeys(ShadowMaterial, _material);

		// Null material will unassign the shadow material:
		if (_material is null)
		{
			ShadowMaterialHandle = ResourceHandle.None;
			ShadowMaterial = null;

			if (hasChanged)
			{
				ReplacementsChanged?.Invoke(this);
			}
			return true;
		}
		if (_material.IsDisposed || !_material.IsLoaded)
		{
			logger.LogError("Cannot set shadow material replacement from disposed or unloaded material resource!");
			return false;
		}


		// Try to retrieve a resource handle for the given material:
		if (!resourceManager.GetResource(_material.resourceKey, out ResourceHandle handle))
		{
			logger.LogError("Cannot find resource handle for shadow material replacement!");
			return false;
		}
		
		ShadowMaterialHandle = handle;
		ShadowMaterial = _material;

		if (hasChanged)
		{
			ReplacementsChanged?.Invoke(this);
		}
		return true;
	}

	#endregion
	#region Methods Resources

	public abstract bool GetResourceSlotIndex(string _slotName, out int _outSlotIndex);
	public abstract bool GetResourceSlotName(int _slotIndex, out string _outSlotName);

	public virtual bool SetResource<T>(string _slotName, T _newValue) where T : class, BindableResource => SetResource(_slotName, (BindableResource)_newValue);
	public abstract bool SetResource(string _slotName, BindableResource _newValue);
	public abstract bool SetResource(string _slotName, ResourceHandle _newValueHandle);

	public virtual bool SetResource<T>(int _slotIndex, T _newValue) where T : class, BindableResource => SetResource(_slotIndex, (BindableResource)_newValue);
	public virtual bool SetResource(int _slotIndex, BindableResource _newValue)
	{
		bool success =
			GetResourceSlotName(_slotIndex, out string slotName) &&
			SetResource(slotName, _newValue);
		return success;
	}
	public virtual bool SetResource(int _slotIndex, ResourceHandle _newValueHandle)
	{
		bool success =
			GetResourceSlotName(_slotIndex, out string slotName) &&
			SetResource(slotName, _newValueHandle);
		return success;
	}


	#endregion
}
