using FragEngine3.Containers;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Renderers.Internal;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Renderers;

/// <summary>
/// A renderer for drawing static polygonal geometry.
/// </summary>
public sealed class StaticMeshRenderer : IPhysicalRenderer
{
	#region Constructors

	public StaticMeshRenderer(GraphicsCore _graphicsCore, string? _rendererName = null)
	{
		GraphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
		logger = GraphicsCore.graphicsSystem.Engine.Logger;
		name = !string.IsNullOrEmpty(_rendererName) ? _rendererName : "StaticMeshRendererInstance";

		// Create object constant buffer immediately:
		BufferDescription cbObjectDesc = new(CBObject.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);
		cbObject = GraphicsCore.MainFactory.CreateBuffer(ref cbObjectDesc);
		cbObject.Name = $"CBObject_{name}";
	}

	~StaticMeshRenderer()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever the renderer's resources (mesh and materials) have been changed.
	/// </summary>
	public event Action<StaticMeshRenderer>? OnResourcesChanged = null;

	/// <summary>
	/// Event that is triggered whenever the '<see cref="RenderMode"/>' has been changed.
	/// This happens when when the material is swapped, or possibly after material resources have finished loading asynchronously.
	/// </summary>
	public event Action<StaticMeshRenderer>? OnRenderModeChanged = null;

	#endregion
	#region Fields

	private readonly Logger logger;

	public readonly string name;

	// Render data:
	private Pose worldPose = Pose.Identity;

	// Resources:
	private Mesh? mesh = null;
	private Material? materialScene = null;
	private Material? materialShadow = null;
	private ResourceHandle shadowMaterialHandle = ResourceHandle.None;

	// Graphics objects:
	private readonly DeviceBuffer cbObject;
	private ResourceSet? resSetObject = null;
	private ResourceSet? overrideBoundResourceSet = null;
	private VersionedMember<Pipeline?> pipelineScene = new(null, 0);
	private VersionedMember<Pipeline?> pipelineShadow = new(null, 0);

	private ushort rendererVersionScene = 0;
	private ushort rendererVersionShadow = 0;
	private CBObject cbObjectData = default;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public GraphicsCore GraphicsCore { get; private init; }

	public ResourceHandle MeshHandle { get; private set; } = ResourceHandle.None;
	public ResourceHandle MaterialHandle { get; private set; } = ResourceHandle.None;
	public ResourceHandle ShadowMaterialHandle
	{
		get => shadowMaterialHandle;
		private set => shadowMaterialHandle = value ?? ResourceHandle.None;
	}

	public bool AreResourcesAssigned { get; private set; } = false;
	public bool AreShadowResourcesAssigned { get; private set; } = false;
	public bool IsVisible => !IsDisposed && AreResourcesAssigned;
	public bool DontDrawUnlessFullyLoaded { get; set; } = false;

	public int LastUpdatedForFrameIdx { get; private set; } = -1;
	public RenderMode RenderMode => materialScene is not null ? materialScene.RenderMode : RenderMode.Opaque;
	public uint LayerFlags { get; set; } = 1;

	public Vector3 VisualCenterPoint => worldPose.position;
	public float BoundingRadius => mesh is not null ? worldPose.scale.X * mesh.BoundingRadius : 0;

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

		cbObject?.Dispose();
		resSetObject?.Dispose();
		pipelineScene.DisposeValue();
		pipelineShadow.DisposeValue();
	}

	/// <summary>
	/// Manually flag the renderer as dirty, forcing a rebuild of the pipeline and constant buffers before the next draw call.
	/// </summary>
	public void MarkDirty()
	{
		LastUpdatedForFrameIdx = 0;
		rendererVersionScene++;
		rendererVersionShadow++;
	}

	public bool SetMesh(string _resourceKey)
	{
		if (string.IsNullOrEmpty(_resourceKey))
		{
			logger.LogError("Cannot assign mesh to static mesh renderer using null or blank resource key!");
			return false;
		}

		ResourceManager resourceManager = GraphicsCore.graphicsSystem.Engine.ResourceManager;

		return resourceManager.GetResource(_resourceKey, out ResourceHandle handle) && SetMesh(handle);
	}

	/// <summary>
	/// Assigns a mesh that shall be drawn by this renderer.
	/// </summary>
	/// <param name="_meshHandle">A resource handle for the mesh. If null or invalid, the mesh will be unassigned.</param>
	/// <returns>True if the mesh was assigned, false otherwise.</returns>
	public bool SetMesh(ResourceHandle? _meshHandle)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot set mesh on disposed renderer!");
			return false;
		}

		string prevResourceKey = MeshHandle?.resourceKey ?? string.Empty;
		mesh = null;

		if (_meshHandle is null || !_meshHandle.IsValid)
		{
			// Assigning a null or invalid handle will unassign current mesh:
			MeshHandle = ResourceHandle.None;
			AreResourcesAssigned = false;
		}
		else
		{
			MeshHandle = _meshHandle;
			if (MeshHandle.IsLoaded)
			{
				mesh = MeshHandle.GetResource<Mesh>();
			}

			AreResourcesAssigned = (materialScene is not null && !materialScene.IsDisposed) || MaterialHandle.IsValid;
		}

		// Notify any users that the renderer's resources have changed:
		if (prevResourceKey != MeshHandle!.resourceKey)
		{
			OnResourcesChanged?.Invoke(this);
		}
		return true;
	}

	public bool SetMaterial(string _resourceKey)
	{
		if (string.IsNullOrEmpty(_resourceKey))
		{
			logger.LogError("Cannot assign material to static mesh renderer using null or blank resource key!");
			return false;
		}

		ResourceManager resourceManager = GraphicsCore.graphicsSystem.Engine.ResourceManager;

		return resourceManager.GetResource(_resourceKey, out ResourceHandle handle) && SetMaterial(handle);
	}

	/// <summary>
	/// Assigns a material for rendering the mesh.
	/// </summary>
	/// <param name="_materialHandle">A resource handle for the material. If null or invalid, the material will be unassigned.</param>
	/// <param name="_overrideShadowMaterial">An override material for rendering shadow maps. If non-null, this material replaces any
	/// shadow material provided by the main material.</param>
	/// <returns>True if the material was assigned, false otherwise.</returns>
	public bool SetMaterial(ResourceHandle? _materialHandle, ResourceHandle? _overrideShadowMaterial = null)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot set material on disposed renderer!");
			return false;
		}

		RenderMode prevRenderMode = RenderMode;
		string prevSceneResourceKey = MaterialHandle.resourceKey ?? string.Empty;
		string prevShadowResourceKey = ShadowMaterialHandle.resourceKey ?? string.Empty;
		materialScene = null;
		materialShadow = null;
		ShadowMaterialHandle = ResourceHandle.None;
		AreShadowResourcesAssigned = false;
		bool isMeshAssigned = (mesh is not null && !mesh.IsDisposed) || MeshHandle.IsValid;

		if (_materialHandle is null || !_materialHandle.IsValid)
		{
			// Assigning a null or invalid handle will unassign current material:
			MaterialHandle = ResourceHandle.None;
			AreResourcesAssigned = false;
		}
		else
		{
			MaterialHandle = _materialHandle;

			// If already loaded, assign scene material immediately:
			if (MaterialHandle.IsLoaded)
			{
				materialScene = MaterialHandle.GetResource<Material>();

				// If no override shadow material is provided, use default from loaded scene material:
				if (materialScene is not null && (_overrideShadowMaterial is null || !_overrideShadowMaterial.IsValid))
				{
					ShadowMaterialHandle = materialScene.ShadowMapMaterialVersion ?? ResourceHandle.None;
					if (ShadowMaterialHandle.IsLoaded)
					{
						materialShadow = ShadowMaterialHandle.GetResource<Material>();
					}

					AreShadowResourcesAssigned = isMeshAssigned;
				}
			}

			AreResourcesAssigned = isMeshAssigned;
		}

		// If an override shadow material is provided, assign it immediately:
		if (_overrideShadowMaterial is not null && _overrideShadowMaterial.IsValid)
		{
			ShadowMaterialHandle = _overrideShadowMaterial;
			if (ShadowMaterialHandle.IsLoaded)
			{
				materialShadow = ShadowMaterialHandle.GetResource<Material>();
			}

			AreShadowResourcesAssigned = isMeshAssigned;
		}

		// Notify any users that the renderer's resources have changed:
		bool materialSceneChanged = prevSceneResourceKey != MaterialHandle!.resourceKey;
		bool materialShadowChanged = prevShadowResourceKey != ShadowMaterialHandle.resourceKey;
		bool renderModeChanged = prevRenderMode != RenderMode;
		if (materialSceneChanged || materialShadowChanged)
		{
			if (materialSceneChanged) rendererVersionScene++;
			if (materialShadowChanged) rendererVersionShadow++;

			OnResourcesChanged?.Invoke(this);
		}
		if (renderModeChanged)
		{
			OnRenderModeChanged?.Invoke(this);
		}
		return true;
	}

	/// <summary>
	/// Assigns a resource set that replaces any bound resource sets provided by the renderer's material.
	/// </summary>
	/// <param name="_newOverrideResourceSet">The resource set to use instead of whatever the material provides.
	/// If null, the override will be unassigned, and the material's set will be used instead.
	/// The renderer does not assume ownership of the given resource set.</param>
	/// <returns>True if the resource set was overriden, or if the override was unassigned. False if an error occured.</returns>
	public bool SetOverrideBoundResourceSet(ResourceSet? _newOverrideResourceSet)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot set override resource set on disposed static mesh renderer!");
			return false;
		}

		if (_newOverrideResourceSet is null || _newOverrideResourceSet.IsDisposed)
		{
			overrideBoundResourceSet = null;
		}
		else
		{
			overrideBoundResourceSet = _newOverrideResourceSet;
		}
		return true;
	}

	public float GetZSortingDepth(Vector3 _viewportPosition, Vector3 _cameraDirection)
	{
		float boundingRadius = mesh is not null ? mesh.BoundingRadius : 0;
		float sortingBias = materialScene is not null ? materialScene.ZSortingBias : 0;
		Vector3 biasPosition = worldPose.position - _cameraDirection * (boundingRadius + sortingBias);

		return Vector3.DistanceSquared(_viewportPosition, biasPosition);
	}

	/// <summary>
	/// Sets a pose that describes the renderer's transformation in world space.
	/// </summary>
	/// <param name="_rendererWorldPose">The pose at which to render the mesh. Details position, rotation, and scale of the object in world space.</param>
	public void SetWorldPose(Pose _rendererWorldPose)
	{
		worldPose = _rendererWorldPose;
	}

	public bool Draw(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx)
	{
		RenderMode prevRenderMode = RenderMode;
		if (!RendererResourceHelper.EnsureResourceIsLoaded(
			MaterialHandle,
			ref materialScene,
			!DontDrawUnlessFullyLoaded,
			out bool proceed,
			out bool materialsChanged))
		{
			return false;
		}

		if (materialsChanged && RenderMode != prevRenderMode)
		{
			OnRenderModeChanged?.Invoke(this);
		}

		return !proceed || Draw_internal(
			_sceneCtx,
			_cameraPassCtx,
			materialScene!,
			rendererVersionScene,
			ref pipelineScene);
	}

	public bool DrawShadowMap(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx)
	{
		RenderMode prevRenderMode = RenderMode;
		if (!RendererResourceHelper.EnsureShadowMaterialIsLoaded(
			MaterialHandle,
			ref shadowMaterialHandle,
			ref materialScene,
			ref materialShadow,
			!DontDrawUnlessFullyLoaded,
			out bool proceed,
			out bool materialsChanged))
		{
			return false;
		}

		if (materialsChanged && RenderMode != prevRenderMode)
		{
			OnRenderModeChanged?.Invoke(this);
		}

		return !proceed || Draw_internal(
			_sceneCtx,
			_cameraPassCtx,
			materialShadow!,
			rendererVersionShadow,
			ref pipelineShadow);
	}

	private bool Draw_internal(
		SceneContext _sceneCtx,
		CameraPassContext _cameraPassCtx,
		Material _material,
		ushort _rendererVersion,
		ref VersionedMember<Pipeline?> _pipeline)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot draw disposed static mesh renderer!");
			return false;
		}

		bool success = true;

		// Ensure the mesh is laoded and ready to use:
		success &= RendererResourceHelper.EnsureResourceIsLoaded(MeshHandle, ref mesh, !DontDrawUnlessFullyLoaded, out bool proceed, out _);
		if (!success || !proceed)
		{
			return success;
		}
		if (!mesh!.Prepare(out DeviceBuffer[]? bufVertices, out DeviceBuffer? bufIndices))
		{
			return false;
		}

		// Update CBObject on the first draw call of each new frame:
		if (LastUpdatedForFrameIdx < _cameraPassCtx.FrameIdx)
		{
			LastUpdatedForFrameIdx = (int)_cameraPassCtx.FrameIdx;
			UpdateCBObject(_cameraPassCtx.CmdList, _sceneCtx.ResLayoutObject);
		}

		// Recreate pipeline if necessary:
		uint expectedPipelineVersion = (uint)(_cameraPassCtx.CameraResourceVersion << 16) | _rendererVersion;
		if (_pipeline.Version != expectedPipelineVersion && !RecreatePipeline(_sceneCtx, _cameraPassCtx, _material, expectedPipelineVersion, ref _pipeline))
		{
			return false;
		}

		// Bind pipeline and resource sets:
		_cameraPassCtx.CmdList.SetPipeline(_pipeline.Value);
		_cameraPassCtx.CmdList.SetGraphicsResourceSet(0, _cameraPassCtx.ResSetCamera);
		_cameraPassCtx.CmdList.SetGraphicsResourceSet(1, resSetObject);

		ResourceSet? boundResourceSet = overrideBoundResourceSet ?? _material.BoundResourceSet;
		if (boundResourceSet is not null && _material.BoundResourceLayout is not null)
		{
			_cameraPassCtx.CmdList.SetGraphicsResourceSet(2, boundResourceSet);
		}

		// Bind geometry buffers:
		for (uint i = 0; i < bufVertices!.Length; i++)
		{
			_cameraPassCtx.CmdList.SetVertexBuffer(i, bufVertices[i]);
		}
		_cameraPassCtx.CmdList.SetIndexBuffer(bufIndices!, mesh.IndexFormat);

		// Issue draw call:
		_cameraPassCtx.CmdList.DrawIndexed(mesh.IndexCount);

		return success;
	}

	private void UpdateCBObject(CommandList _cmdList, in ResourceLayout _resLayoutObject)
	{
		// Update data in the object's constant buffer:
		cbObjectData = new()
		{
			mtxLocal2World = worldPose.Matrix,
			worldPosition = worldPose.position,
			boundingRadius = mesh!.BoundingRadius,
		};

		_cmdList.UpdateBuffer(cbObject, 0, ref cbObjectData);

		// Ensure the object's resource set has been created:
		if (resSetObject is null)
		{
			ResourceSetDescription resSetObjectDesc = new(
				_resLayoutObject,
				cbObject);

			resSetObject = GraphicsCore.MainFactory.CreateResourceSet(ref resSetObjectDesc);
			resSetObject.Name = $"ResSetObject_{name}";

			// Mark renderer as dirty since we have a new resource set:
			rendererVersionScene++;
			rendererVersionShadow++;
		}
	}

	private bool RecreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraPassCtx, Material _material, uint _newestVersion, ref VersionedMember<Pipeline?> _pipeline)
	{
		_pipeline.DisposeValue();

		if (!_material.CreatePipeline(_sceneCtx, _cameraPassCtx, _newestVersion, mesh!.VertexDataFlags, out PipelineState pipelineState))
		{
			_pipeline.UpdateValue(0, null);
			logger.LogError($"Failed to retrieve pipeline description for material '{_material}'!");
			return false;
		}

		_pipeline.UpdateValue(_newestVersion, pipelineState.pipeline);  //TODO: Ditch pipeline state object and type.
		return true;
	}

	#endregion
}
