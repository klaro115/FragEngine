using System.Numerics;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using Veldrid;

namespace FragEngine3.Graphics.Components;

public sealed class StaticMeshRendererNew : Component, IRenderer
{
	#region Constructors

	public StaticMeshRendererNew(SceneNode _node) : base(_node)
	{
		graphicsCore = _node?.scene.engine.GraphicsSystem.graphicsCore ??  throw new ArgumentNullException(nameof(_node), "Node and graphics core may not be null!");

		// Create object constant buffer immediately:
		BufferDescription cbObjectDesc = new(CBObject.packedByteSize, BufferUsage.Dynamic);
		cbObject = graphicsCore.MainFactory.CreateBuffer(ref cbObjectDesc);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever the renderer's resources (mesh and materials) have been changed.
	/// </summary>
	public event Action<StaticMeshRendererNew>? OnResourcesChanged = null;

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;

	// Resources:
	private Mesh? mesh = null;
	private Material? materialScene = null;
	private Material? materialShadow = null;

	// Graphics objects:
	private readonly DeviceBuffer cbObject;
	private Pipeline? pipelineScene = null;
	private Pipeline? pipelineShadow = null;

	private uint lastUpdatedForFrameIdx = 0;
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
		pipelineScene?.Dispose();
		pipelineShadow?.Dispose();
	}

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
		if (prevSceneResourceKey != MaterialHandle!.resourceKey ||
			prevShadowResourceKey != ShadowMaterialHandle.resourceKey)
		{
			OnResourcesChanged?.Invoke(this);
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
			ref pipelineScene);
	}

	public bool DrawShadowMap(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx)
	{
		if (!EnsureResourceIsLoaded(ShadowMaterialHandle, ref materialShadow, out bool proceed))
		{
			return false;
		}

		return !proceed || Draw_internal(
			_sceneCtx,
			_cameraPassCtx,
			materialShadow!,
			ref pipelineShadow);
	}

	private bool Draw_internal(
		SceneContext _sceneCtx,
		CameraPassContext _cameraPassCtx,
		Material _material,
		ref Pipeline? _pipeline)
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
			lastUpdatedForFrameIdx = _cameraPassCtx.frameIdx;
			UpdateCBObject(_cameraPassCtx.cmdList);
		}

		//TODO: Recreate pipeline if necessary.
		//TODO: Issue draw calls.

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

	private void UpdateCBObject(CommandList _cmdList)
	{
		Pose worldPose = node.WorldTransformation;

		cbObjectData = new()
		{
			mtxLocal2World = worldPose.Matrix,
			worldPosition = worldPose.position,
			boundingRadius = mesh!.BoundingRadius,
		};

		_cmdList.UpdateBuffer(cbObject, 0, ref cbObjectData);
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		throw new NotImplementedException();
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		throw new NotImplementedException();
	}

	#endregion
}
