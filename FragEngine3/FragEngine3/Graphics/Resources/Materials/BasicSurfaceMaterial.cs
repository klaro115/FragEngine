using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials.Internal;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

public sealed class BasicSurfaceMaterial : SurfaceMaterial
{
	#region Constructors

	public BasicSurfaceMaterial(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_graphicsCore, _resourceHandle, _data)
	{
		if (!_data.CreateLayoutFromBoundResources(graphicsCore, out resLayoutUserBound, []))
		{
			Dispose();
			throw new Exception($"Failed to create resource layout for non-system resources! (Resource key: '{resourceKey}')");
		}
		if (!_data.CreateBindingSlotsFromBoundResources(MarkDirty, 0, out resourceSlotsUserBound, out resourcesUserBound!))
		{
			Dispose();
			throw new Exception($"Failed to create resource slots and buffers for non-system resources! (Resource key: '{resourceKey}')");
		}

		// Initialize and assign resources that are identified in material data immediately:
		string?[] boundResourceKeys = _data.GetBoundResourceKeys();
		InitializeBoundResourceSlots(resourceSlotsUserBound, boundResourceKeys, false);
	}

	#endregion
	#region Fields

	private bool isDirty = true;

	private readonly ResourceLayout? resLayoutUserBound = null;
	private ResourceSet? resSetUserBound = null;

	private readonly Dictionary<string, MaterialUserBoundResourceSlot> resourceSlotsUserBound;
	private readonly BindableResource[] resourcesUserBound;

	#endregion
	#region Properties

	public ResourceLayout BoundResourceLayout => resLayoutUserBound!;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		IsDisposed = true;

		resLayoutUserBound?.Dispose();
		resSetUserBound?.Dispose();
	}

	public override void MarkDirty() => isDirty = true;

	public override bool CreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, MeshVertexDataFlags _vertexDataFlags, out PipelineState? _outPipelineState, out bool _outIsFullyLoaded)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot create pipeline for default surface material that has already been disposed!");
			_outPipelineState = null;
			_outIsFullyLoaded = false;
			return false;
		}

		if (!GetOrCreateShaderSet(_vertexDataFlags, out ShaderSetDescription shaderSetDesc, out _outIsFullyLoaded))
		{
			logger.LogError($"Failed to create shader set description for default surface material '{resourceKey}' and vertex variant '{_vertexDataFlags}'!");
			_outPipelineState = null;
			return false;
		}

		// If underlying resources are not ready to draw yet, exit now:
		if (!_outIsFullyLoaded)
		{
			_outPipelineState = null;
			return true;
		}

		if (!CreateResourceLayouts(in _sceneCtx, out ResourceLayout[]? resourceLayouts))
		{
			logger.LogError($"Failed to prepare resource layouts for default surface material '{resourceKey}' and vertex variant '{_vertexDataFlags}'!");
			_outPipelineState = null;
			return false;
		}

		// Try to create pipeline:
		Pipeline pipeline;
		try
		{
			GraphicsPipelineDescription pipelineDesc = new(
				blendState,
				depthStencilState,
				rasterizerState,
				PrimitiveTopology.TriangleList,
				shaderSetDesc,
				resourceLayouts,
				_cameraCtx.OutputDesc,
				ResourceBindingModel.Default);

			pipeline = graphicsCore.MainFactory.CreateGraphicsPipeline(ref pipelineDesc);
			pipeline.Name = $"Pipeline_{resourceKey}_V{(int)_vertexDataFlags:b}";
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create pipeline for default surface material '{resourceKey}' and vertex variant '{_vertexDataFlags}'!", ex);
			_outPipelineState = null;
			return false;
		}

		// Create pipeline state object and return success:
		uint vertexBufferCount = (uint)shaderSetDesc.VertexLayouts.Length;

		_outPipelineState = new(
			pipeline,
			0u,
			_vertexDataFlags,
			vertexBufferCount);
		return true;
	}

	public override bool Prepare(in SceneContext _sceneCtx, in CameraPassContext _cameraPassCtx, ResourceSet? _resSetObject, ref ResourceSet[]? _resourceSets)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot prepare default surface material that has already been disposed!");
			_resourceSets = null;
			return false;
		}

		bool hasResourceSetChanged = _resourceSets is null || _resourceSets.Length != 3;

		// Update bound resource sets:
		if (isDirty)
		{
			// Note: The array of user-bound resource (i.e. `resourcesUserBound`) is updated automatically through
			// resource slots. By assigning a value to a slot, the new value is automatically updated on the slot's
			// mapped array index position.

			resSetUserBound?.Dispose();
			if (!CreateResourceSetForBoundResources(resLayoutUserBound!, out resSetUserBound, resourcesUserBound!))
			{
				return false;
			}
			isDirty = false;
			hasResourceSetChanged = true;

			if (!InitializeBoundResourceSlots(resourceSlotsUserBound, null, true))
			{
				return false;
			}
		}

		// (Re)allocate and populate resource sets array:
		if (hasResourceSetChanged && !RecreateResourceSets(in _cameraPassCtx, _resSetObject, ref _resourceSets))
		{
			return false;
		}

		return true;
	}

	private bool CreateResourceLayouts(in SceneContext _sceneCtx, out ResourceLayout[] _outResourceLayouts)
	{
		if (resLayoutUserBound is not null)
		{
			_outResourceLayouts =
			[
				_sceneCtx.ResLayoutCamera,
				_sceneCtx.ResLayoutObject,
				resLayoutUserBound,
			];
		}
		else
		{
			_outResourceLayouts =
			[
				_sceneCtx.ResLayoutCamera,
				_sceneCtx.ResLayoutObject,
			];
		}
		return true;
	}

	private bool RecreateResourceSets(in CameraPassContext _cameraPassCtx, ResourceSet? _resSetObject, ref ResourceSet[]? _resourceSets)
	{
		if (_resSetObject is null || _resSetObject.IsDisposed)
		{
			logger.LogError($"Cannot recreate resource sets array for default surface material '{resourceKey}' using null or disposed object resource set!");
			return false;
		}
		if (_resourceSets is null || _resourceSets.Length != 3)
		{
			_resourceSets = new ResourceSet[3];
		}

		_resourceSets[0] = _cameraPassCtx.ResSetCamera;
		_resourceSets[1] = _resSetObject;
		_resourceSets[2] = resSetUserBound!;

		return true;
	}

	#endregion
	#region Methods Resources

	public override IEnumerator<ResourceHandle> GetResourceDependencies()
	{
		// Enumerate shaders:
		yield return VertexShaderHandle;
		if (GeometryShaderHandle.IsValid)
		{
			yield return GeometryShaderHandle;
		}
		if (TesselationCtrlShaderHandle.IsValid &&
			TesselationEvalShaderHandle.IsValid)
		{
			yield return TesselationCtrlShaderHandle;
			yield return TesselationEvalShaderHandle;
		}
		yield return PixelShaderHandle;

		// Enumerate all user-bound resources:
		foreach (var kvp in resourceSlotsUserBound)
		{
			string? boundResourceKey = kvp.Value.ResourceKey;
			if (!string.IsNullOrEmpty(boundResourceKey) && resourceManager.GetResource(boundResourceKey, out ResourceHandle boundResourceHandle))
			{
				yield return boundResourceHandle;
			}
		}

		// Return self:
		if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
		{
			yield return handle;
		}
	}

	public override bool GetResourceSlotIndex(string _slotName, out int _outSlotIndex)
	{
		if (!string.IsNullOrEmpty(_slotName) && resourceSlotsUserBound.TryGetValue(_slotName, out MaterialUserBoundResourceSlot? slot))
		{
			_outSlotIndex = slot.boundResourceIndex;
			return true;
		}
		_outSlotIndex = -1;
		return false;
	}

	public override bool GetResourceSlotName(int _slotIndex, out string _outSlotName)
	{
		foreach (var kvp in resourceSlotsUserBound)
		{
			if (kvp.Value.boundResourceIndex == _slotIndex)
			{
				_outSlotName = kvp.Key;
				return true;
			}
		}
		_outSlotName = string.Empty;
		return false;
	}

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
