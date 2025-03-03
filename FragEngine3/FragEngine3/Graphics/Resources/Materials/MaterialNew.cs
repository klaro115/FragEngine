using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

/// <summary>
/// Base class for materials. Materials are graphics resources that define a combination shaders, textures,
/// and constant buffers. They are used to create and manage resource sets which can be bounds to a graphics
/// pipeline by an implementation of <see cref="IRenderer"/>. The renderer creates the <see cref="Pipeline"/>,
/// but the material provides the resources and bindings for it.
/// </summary>
public abstract class MaterialNew : Resource
{
	#region Constructors

	protected MaterialNew(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_resourceHandle)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
		logger = graphicsCore.graphicsSystem.Engine.Logger;
		materialType = _data.MaterialType;
	}

	~MaterialNew()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;
	protected readonly Logger logger;

	public readonly MaterialType materialType;

	protected ConstantBufferSlot[] customConstantBufferSlots = null!;

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
	public MaterialNew? ShadowMaterial { get; private set; } = null;

	/// <summary>
	/// Gets a resource handle for the replacement material that's used to render simplified versions or distant LODs of this material.
	/// </summary>
	public ResourceHandle SimplifiedMaterialHandle{ get; private set; } = ResourceHandle.None;
	/// <summary>
	/// Gets the replacement material that's used to render simplified versions or distant LODs of this material.
	/// </summary>
	public MaterialNew? SimplifiedMaterial { get; private set; } = null;

	#endregion
	#region Methods

	/// <summary>
	/// Creates a new graphics pipeline for a specific renderer's use-case.
	/// </summary>
	/// <param name="_sceneCtx">A context object with rendering data for the current scene.</param>
	/// <param name="_cameraCtx">A context object with rendering data for the current camera pass.</param>
	/// <param name="_vertexDataFlags">Bit flags of all vertex data flags that are available and used by the renderer.
	/// For standard surface materials, at least the '<see cref="MeshVertexDataFlags.BasicSurfaceData"/>' flag must be raised.
	/// For compute shader materials, this value is ignored.</param>
	/// <param name="_outPipelineState">Outputs a new pipeline state object for the specified vertex data configuration, or null on failure.</param>
	/// <returns>True if the pipeline could be (re)created and is ready for binding to the graphics device, false otherwise.</returns>
	public abstract bool CreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, MeshVertexDataFlags _vertexDataFlags, out PipelineState? _outPipelineState);

	/// <summary>
	/// Prepares that material's resources for rendering and binds resources and shaders to the graphics pipeline.
	/// </summary>
	/// <param name="_sceneCtx">A context object with rendering data for the current scene.</param>
	/// <param name="_cameraCtx">A context object with rendering data for the current camera pass.</param>
	/// <param name="_outResourceSets">Outputs an array of all resource sets used by this material.</param>
	/// <returns>True if the material could be prepared, and resources are loaded for imminent rendering, false otherwise.</returns>
	public abstract bool Prepare(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, out ResourceSet[]? _outResourceSets);

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
		if (_boundResources is null || _boundResources.Length == 0)
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
		// Null or invalid handles will unassign the shadow material:
		if (_handle is null || !_handle.IsValid)
		{
			ShadowMaterialHandle = ResourceHandle.None;
			ShadowMaterial = null;
			return true;
		}

		ShadowMaterialHandle = _handle;
		ShadowMaterial = ShadowMaterialHandle.GetResource<MaterialNew>(_loadImmediately);

		// Unassign shadow material if loading has failed:
		if (_loadImmediately && ShadowMaterial is null)
		{
			logger.LogError($"Failed to load shadow material replacement '{_handle}'!");
			ShadowMaterialHandle = ResourceHandle.None;
			return false;
		}
		return true;
	}

	/// <summary>
	/// Assigns a replacement material that'll be used to draw shadow maps for this material.
	/// </summary>
	/// <param name="_material">The replacement material.</param>
	/// <returns>True if the new replacement material could be assigned, false otherwise.</returns>
	public bool SetShadowMaterial(MaterialNew? _material)
	{
		// Null material will unassign the shadow material:
		if (_material is null)
		{
			ShadowMaterialHandle = ResourceHandle.None;
			ShadowMaterial = null;
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
		return true;
	}

	#endregion
	#region Methods Resources

	public virtual bool SetResource<T>(string _slotName, T _newValue) where T : class, BindableResource => SetResource(_slotName, (BindableResource)_newValue);
	public abstract bool SetResource(string _slotName, BindableResource _newValue);
	public abstract bool SetResource(string _slotName, ResourceHandle _newValueHandle);

	#endregion
}
