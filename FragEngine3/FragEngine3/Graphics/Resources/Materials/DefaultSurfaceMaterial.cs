using FragEngine3.Graphics.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials.Internal;
using FragEngine3.Graphics.Utility;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

public sealed class DefaultSurfaceMaterial : MaterialNew
{
	#region Types

	[Flags]
	private enum DirtyFlags
	{
		CBDefaultSurface	= 1,
		BoundResources		= 2,
		//...

		ALL					= CBDefaultSurface | BoundResources
	}

	#endregion
	#region Constructors

	public DefaultSurfaceMaterial(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_graphicsCore, _resourceHandle, _data)
	{
		if (!ConstantBufferUtility.CreateOrUpdateConstantBuffer(graphicsCore, resourceKey, CBDefaultSurface.byteSize, ref cbDefaultSurface, ref cbDefaultSurfaceData))
		{
			throw new Exception($"Failed to create default surface constant buffer! (Resource key: '{resourceKey}')");
		}
		if (!_data.CreateLayoutFromBoundResources(graphicsCore, out resLayoutUserBound))
		{
			Dispose();
			throw new Exception($"Failed to create resource layout for non-system resources! (Resource key: '{resourceKey}')");
		}
		if (!_data.CreateBindingSlotsFromBoundResources(MarkResSetUserBoundDirty, out resourceSlotsUserBound, out resourcesUserBound))
		{
			Dispose();
			throw new Exception($"Failed to create resource slots and buffers for non-system resources! (Resource key: '{resourceKey}')");
		}
	}

	#endregion
	#region Fields

	private DirtyFlags dirtyFlags = DirtyFlags.ALL;

	private CBDefaultSurface cbDefaultSurfaceData = default;
	private DeviceBuffer? cbDefaultSurface = null;

	private ResourceLayout? resLayoutUserBound = null;
	private ResourceSet? resSetUserBound = null;

	private Dictionary<string, MaterialUserBoundResourceSlot> resourceSlotsUserBound;
	private BindableResource?[] resourcesUserBound;

	private ResourceSet[] resourceSets = [];

	#endregion
	#region Properties

	//...

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		IsDisposed = true;

		cbDefaultSurface?.Dispose();
		resLayoutUserBound?.Dispose();
		resSetUserBound?.Dispose();

		//...
	}

	public void MarkDirty() => dirtyFlags = DirtyFlags.ALL;
	private void MarkResSetUserBoundDirty() => dirtyFlags |= DirtyFlags.BoundResources;

	public override bool CreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, MeshVertexDataFlags _vertexDataFlags, out PipelineState? _outPipelineState)
	{
		//TODO
		throw new NotImplementedException();
	}

	public override bool Prepare(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, out ResourceSet[]? _outResourceSets)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot prepare default surface material that has already been disposed!");
			_outResourceSets = null;
			return false;
		}

		// Update constant buffers:
		if (dirtyFlags.HasFlag(DirtyFlags.CBDefaultSurface))
		{
			if (!ConstantBufferUtility.UpdateConstantBuffer(graphicsCore, resourceKey, ref cbDefaultSurface!, ref cbDefaultSurfaceData))
			{
				_outResourceSets = null;
				return false;
			}
			dirtyFlags &= ~DirtyFlags.CBDefaultSurface;
		}

		// Update bound resource sets:
		if (dirtyFlags.HasFlag(DirtyFlags.BoundResources))
		{
			resSetUserBound?.Dispose();
			if (!CreateResourceSetForBoundResources(resLayoutUserBound!, out resSetUserBound, resourcesUserBound!))
			{
				_outResourceSets = null;
				return false;
			}
			dirtyFlags &= ~DirtyFlags.BoundResources;
		}

		_outResourceSets = resourceSets;
		return true;
	}

	public override IEnumerator<ResourceHandle> GetResourceDependencies()
	{
		//TODO: Yield all referenced textures, buffers, samplers, and other resources.

		if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
		{
			yield return handle;
		}
	}

	#endregion
	#region Methods Resources

	private bool GetUserBoundResourceSlot(string _slotName, out MaterialUserBoundResourceSlot? _slot)
	{
		if (IsDisposed)
		{
			logger.LogError($"Cannot set value of user-bound resource slot of disposed default surface material!");
			_slot = null;
			return false;
		}
		if (string.IsNullOrEmpty(_slotName))
		{
			logger.LogError($"Cannot set value of user-bound resource slot of default surface material '{resourceKey}' using null or blank slot name!");
			_slot = null;
			return false;
		}
		if (!resourceSlotsUserBound.TryGetValue(_slotName, out _slot))
		{
			logger.LogWarning($"Default surface material '{resourceKey}' does not have a user-bound resource slot named '{_slotName}'");
			return false;
		}
		return true;
	}

	public override bool SetResource<T>(string _slotName, T _newValue)
	{
		if (!GetUserBoundResourceSlot(_slotName, out MaterialUserBoundResourceSlot? slot))
		{
			return false;
		}
		if (slot is not MaterialUserBoundResourceSlot<T> typedSlot)
		{
			logger.LogError($"Type mismatch on user-bound resource slot named '{_slotName}' of default surface material '{resourceKey}'! ()");
			return false;
		}

		typedSlot.Value = _newValue;
		return true;
	}

	public override bool SetResource(string _slotName, BindableResource _newValue)
	{
		if (!GetUserBoundResourceSlot(_slotName, out MaterialUserBoundResourceSlot? slot))
		{
			return false;
		}

		bool success = slot!.SetValue(_newValue);
		return success;
	}

	public override bool SetResource(string _slotName, ResourceHandle _newValueHandle)
	{
		if (!GetUserBoundResourceSlot(_slotName, out MaterialUserBoundResourceSlot? slot))
		{
			return false;
		}

		bool success = slot!.SetValue(_newValueHandle);
		return success;
	}

	#endregion
}
