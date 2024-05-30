using System.Numerics;
using FragEngine3.Containers;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components;

public sealed class StaticMeshRenderer : Component, IRenderer
{
	#region Constructors

	public StaticMeshRenderer(SceneNode _node) : base(_node)
	{
		graphicsCore = _node?.scene.engine.GraphicsSystem.graphicsCore ??  throw new ArgumentNullException(nameof(_node), "Node and graphics core may not be null!");

		// Create object constant buffer immediately:
		BufferDescription cbObjectDesc = new(CBObject.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);
		cbObject = graphicsCore.MainFactory.CreateBuffer(ref cbObjectDesc);
		cbObject.Name = $"CBObject_{node.Name}";
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever the renderer's resources (mesh and materials) have been changed.
	/// </summary>
	public event Action<StaticMeshRenderer>? OnResourcesChanged = null;

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;

	// Resources:
	private Mesh? mesh = null;
	private Material? materialScene = null;
	private Material? materialShadow = null;

	// Graphics objects:
	private readonly DeviceBuffer cbObject;
	private ResourceSet? resSetObject = null;
	private ResourceSet? overrideBoundResourceSet = null;
	private VersionedMember<Pipeline?> pipelineScene = new(null, 0);
	private VersionedMember<Pipeline?> pipelineShadow = new(null, 0);

	private int lastUpdatedForFrameIdx = -1;
	private ushort rendererVersionScene = 0;
	private ushort rendererVersionShadow = 0;
	private CBObject cbObjectData = default;

	#endregion
	#region Properties

	public GraphicsCore GraphicsCore => graphicsCore;

	public ResourceHandle MeshHandle { get; private set; } = ResourceHandle.None;
	public ResourceHandle MaterialHandle { get; private set; } = ResourceHandle.None;
	public ResourceHandle ShadowMaterialHandle { get; private set; } = ResourceHandle.None;

	public bool AreResourcesAssigned { get; private set; } = false;
	public bool AreShadowResourcesAssigned { get; private set; } = false;
	public bool IsVisible => !IsDisposed && node.IsEnabled && AreResourcesAssigned;
	public bool DontDrawUnlessFullyLoaded { get; set; } = false;

	public RenderMode RenderMode => materialScene is not null ? materialScene.RenderMode : RenderMode.Opaque;
	public uint LayerFlags { get; set; } = 1;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

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
		lastUpdatedForFrameIdx = 0;
		rendererVersionScene++;
		rendererVersionShadow++;
	}

	public bool SetMesh(string _resourceKey)
	{
		if (string.IsNullOrEmpty(_resourceKey))
		{
			Logger.LogError("Cannot assign mesh to static mesh renderer using null or blank resource key!");
			return false;
		}

		ResourceManager resourceManager = graphicsCore.graphicsSystem.engine.ResourceManager;

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
			Logger.LogError("Cannot set mesh on disposed renderer!");
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

			AreResourcesAssigned = (materialScene is not null && !materialScene.IsDisposed) || MaterialHandle.IsValid;
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
			Logger.LogError("Cannot assign material to static mesh renderer using null or blank resource key!");
			return false;
		}

		ResourceManager resourceManager = graphicsCore.graphicsSystem.engine.ResourceManager;

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
			Logger.LogError("Cannot set material on disposed renderer!");
			return false;
		}

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
		if (materialSceneChanged || materialShadowChanged)
		{
			if (materialSceneChanged) rendererVersionScene++;
			if (materialShadowChanged) rendererVersionShadow++;

			OnResourcesChanged?.Invoke(this);
		}
		return true;
	}

	public bool SetOverrideBoundResourceSet(ResourceSet? _newOverrideResourceSet)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot set override resource set on disposed static mesh renderer!");
			return false;
		}

		if (_newOverrideResourceSet == null || _newOverrideResourceSet.IsDisposed)
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
		Vector3 biasPosition = node.WorldPosition - _cameraDirection * (boundingRadius + sortingBias);

		return Vector3.DistanceSquared(_viewportPosition, biasPosition);
	}

	public bool Draw(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx)
	{
		if (!EnsureResourceIsLoaded(MaterialHandle, ref materialScene, out bool proceed))
		{
			return false;
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
		if (!EnsureShadowMaterialIsLoaded(out bool proceed))
		{
			return false;
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
			Logger.LogError("Cannot draw disposed static mesh renderer!");
			return false;
		}

		bool success = true;

		// Ensure the mesh is laoded and ready to use:
		success &= EnsureResourceIsLoaded(MeshHandle, ref mesh, out bool proceed);
		if (!success || !proceed)
		{
			return success;
		}
		if (!mesh!.Prepare(out DeviceBuffer[] bufVertices, out DeviceBuffer bufIndices))
		{
			return false;
		}

		// Update CBObject on the first draw call of each new frame:
		if (lastUpdatedForFrameIdx < _cameraPassCtx.frameIdx)
		{
			lastUpdatedForFrameIdx = (int)_cameraPassCtx.frameIdx;
			UpdateCBObject(_cameraPassCtx.cmdList, in _sceneCtx.resLayoutObject);
		}

		// Recreate pipeline if necessary:
		uint expectedPipelineVersion = (uint)(_cameraPassCtx.cameraResourceVersion << 16) | _rendererVersion;
		if (_pipeline.Version != expectedPipelineVersion && !RecreatePipeline(_sceneCtx, _cameraPassCtx, _material, expectedPipelineVersion, ref _pipeline))
		{
			return false;
		}

		// Bind pipeline and resource sets:
		_cameraPassCtx.cmdList.SetPipeline(_pipeline.Value);
		_cameraPassCtx.cmdList.SetGraphicsResourceSet(0, _cameraPassCtx.resSetCamera);
		_cameraPassCtx.cmdList.SetGraphicsResourceSet(1, resSetObject);

		ResourceSet? boundResourceSet = overrideBoundResourceSet ?? _material.BoundResourceSet;
		if (boundResourceSet != null && _material.BoundResourceLayout != null)
		{
			_cameraPassCtx.cmdList.SetGraphicsResourceSet(2, boundResourceSet);
		}

		// Bind geometry buffers:
		for (uint i = 0; i < bufVertices.Length; i++)
		{
			_cameraPassCtx.cmdList.SetVertexBuffer(i, bufVertices[i]);
		}
		_cameraPassCtx.cmdList.SetIndexBuffer(bufIndices, mesh.IndexFormat);

		// Issue draw call:
		_cameraPassCtx.cmdList.DrawIndexed(mesh.IndexCount);

		return success;
	}

	private bool EnsureResourceIsLoaded<T>(ResourceHandle _handle, ref T? _resource, out bool _outProceed) where T : Resource
	{
		bool success = true;
		_outProceed = true;

		if (_resource is null)
		{
			if (_handle is null || !_handle.IsValid)
			{
				Logger.LogError($"Resource handle of mesh renderer '{node.Name}' is null or invalid!");
				_outProceed = false;
				return false;
			}

			bool loadImmediately = !DontDrawUnlessFullyLoaded;
			_resource = _handle.GetResource<T>(loadImmediately);
			_outProceed = _resource is not null;

			if (loadImmediately && !_outProceed)
			{
				Logger.LogError($"Failed to load resource '{_handle.resourceKey}' for static mesh renderer!");
				success = false;
			}
		}
		return success;
	}

	private bool EnsureShadowMaterialIsLoaded(out bool _outProceed)
	{
		bool success = true;
		_outProceed = true;

		if (materialShadow is null)
		{
			if (ShadowMaterialHandle is null || !ShadowMaterialHandle.IsValid)
			{
				// Get shadow material from scene material:
				if (!EnsureResourceIsLoaded(MaterialHandle, ref materialScene, out _outProceed))
				{
					return false;
				}
				if (!_outProceed || !materialScene!.HasShadowMapMaterialVersion)
				{
					_outProceed = false;
					return true;
				}

				ShadowMaterialHandle = materialScene.ShadowMapMaterialVersion ?? ResourceHandle.None;
			}

			bool loadImmediately = !DontDrawUnlessFullyLoaded;
			materialShadow = ShadowMaterialHandle.GetResource<Material>(loadImmediately);
			_outProceed = materialShadow is not null;

			if (loadImmediately && !_outProceed)
			{
				Logger.LogError($"Failed to load shadow material '{ShadowMaterialHandle.resourceKey}' for static mesh renderer!");
				success = false;
			}
		}
		return success;
	}

	private void UpdateCBObject(CommandList _cmdList, in ResourceLayout _resLayoutObject)
	{
		// Update data in the object's constant buffer:
		Pose worldPose = node.WorldTransformation;

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

			resSetObject = graphicsCore.MainFactory.CreateResourceSet(ref resSetObjectDesc);
			resSetObject.Name = $"ResSetObject_{node.Name}";

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
			Logger.LogError($"Failed to retrieve pipeline description for material '{_material}'!");
			return false;
		}

		_pipeline.UpdateValue(_newestVersion, pipelineState.pipeline);	//TODO: Ditch pipeline state object and type.
		return true;
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
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

		ResourceManager resourceManager = graphicsCore.graphicsSystem.engine.ResourceManager;

		// Reset all resource references:
		MaterialHandle = ResourceHandle.None;
		ShadowMaterialHandle = ResourceHandle.None;
		materialScene = null;
		materialShadow = null;
		MeshHandle = ResourceHandle.None;
		mesh = null;

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
		StaticMeshRendererData data = new()
		{
			Mesh = MeshHandle?.resourceKey ?? string.Empty,
			Material = MaterialHandle?.resourceKey ?? string.Empty,
			ShadowMaterial = ShadowMaterialHandle is not null && ShadowMaterialHandle != materialScene?.ShadowMapMaterialVersion
				? ShadowMaterialHandle.resourceKey
				: string.Empty,

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

	#endregion
}
