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
		graphicsCore = _node.scene.engine.GraphicsSystem.graphicsCore;
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

	private Mesh? mesh = null;
	private Material? materialScene = null;
	private Material? materialShadow = null;

	private DeviceBuffer? cbObject = null;
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

	private bool HasMesh => (mesh is not null && mesh.IsInitialized) || (MeshHandle is not null && MeshHandle.IsValid);
	private bool HasMaterial => (materialScene is not null && !materialScene.IsDisposed) || (MaterialHandle is not null && MaterialHandle.IsValid);

	public bool IsVisible => !IsDisposed && node.IsEnabled && HasMesh && HasMaterial;
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
			if (_handle is null)
			{
				Logger.LogError($"Resource handle of mesh renderer '{node.Name}' has not been set!");
				_outProceed = false;
				return false;
			}

			bool loadImmediately = !DontDrawUnlessFullyLoaded;
			_resource = _handle.GetResource<T>(loadImmediately);
			_outProceed = _resource is not null;

			if (loadImmediately && !_outProceed)
			{
				Logger.LogError("Failed to load resource for static mesh renderer!");
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
