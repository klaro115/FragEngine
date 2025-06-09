using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials.Internal;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

public abstract class ComputeMaterial : Material
{
	#region Constructors

	public ComputeMaterial(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_graphicsCore, _resourceHandle, _data)
	{
		// Load resource handles for vertex and pixel shaders:
		if (!resourceManager.GetResource(_data.Shaders.Compute, out ResourceHandle handleCS))
		{
			throw new Exception("Compute shader of compute material could not be found!");
		}
		ComputeShaderHandle = handleCS;

		if (handleCS.IsLoaded)
		{
			computeShader = handleCS.GetResource<ShaderResource>(false, false);
		}

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

	protected ShaderResource? computeShader = null;

	private readonly ResourceLayout? resLayoutUserBound = null;
	private ResourceSet? resSetUserBound = null;

	private readonly Dictionary<string, MaterialUserBoundResourceSlot> resourceSlotsUserBound;
	private readonly BindableResource[] resourcesUserBound;

	#endregion
	#region Properties

	/// <summary>
	/// Resource handle for the material's vertex shader. This should never be null or invalid.
	/// </summary>
	public ResourceHandle ComputeShaderHandle { get; protected set; } = ResourceHandle.None;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		IsDisposed = true;

		resLayoutUserBound?.Dispose();
		resSetUserBound?.Dispose();
	}

	public void MarkDirty() => isDirty = true;

	public override bool CreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, MeshVertexDataFlags _vertexDataFlags, out PipelineState? _outPipelineState, out bool _outIsFullyLoaded)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot create pipeline for compute material that has already been disposed!");
			_outPipelineState = null;
			_outIsFullyLoaded = false;
			return false;
		}

		if (!GetOrLoadShaderProgram(_vertexDataFlags, out Shader? shaderProgram, out _outIsFullyLoaded))
		{
			logger.LogError($"Failed to create shader set description for compute material '{resourceKey}' and vertex variant '{_vertexDataFlags}'!");
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
			logger.LogError($"Failed to prepare resource layouts for compute material '{resourceKey}' and vertex variant '{_vertexDataFlags}'!");
			_outPipelineState = null;
			return false;
		}

		// Try to create pipeline:
		Pipeline pipeline;
		try
		{
			ComputePipelineDescription pipelineDesc = new(
				shaderProgram,
				resourceLayouts,
				8,
				8,
				8);

			pipeline = graphicsCore.MainFactory.CreateComputePipeline(ref pipelineDesc);
			pipeline.Name = $"Pipeline_{resourceKey}_V{(int)_vertexDataFlags:b}";
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create pipeline for compute material '{resourceKey}' and vertex variant '{_vertexDataFlags}'!", ex);
			_outPipelineState = null;
			return false;
		}

		// Create pipeline state object and return success:
		_outPipelineState = new(
			pipeline,
			0u,
			_vertexDataFlags,
			0u);
		return true;
	}

	public override bool Prepare(in SceneContext _sceneCtx, in CameraPassContext _cameraPassCtx, ResourceSet? _resSetObject, ref ResourceSet[]? _resourceSets)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot prepare compute material that has already been disposed!");
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

	protected bool GetOrLoadShaderProgram(MeshVertexDataFlags _vertexFlags, out Shader? _outShaderProgram, out bool _outIsFullyLoaded)
	{
		if (computeShader is null || computeShader.IsDisposed)
		{
			if (ComputeShaderHandle is null || !ComputeShaderHandle.IsValid)
			{
				logger.LogError("Resource handle of compute shader is not assigned or invalid!");
				_outShaderProgram = null!;
				_outIsFullyLoaded = false;
				return false;
			}

			computeShader = ComputeShaderHandle.GetResource<ShaderResource>(false, true);
		}
		_outIsFullyLoaded = computeShader is not null && computeShader.IsLoaded;
		if (!_outIsFullyLoaded)
		{
			_outShaderProgram = null!;
			_outIsFullyLoaded = false;
			return true;
		}

		bool success = computeShader!.GetShaderProgram(_vertexFlags, out _outShaderProgram);
		return success;
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

	/// <summary>
	/// Initializes a set of bound resource slots from their current values.
	/// </summary>
	/// <param name="resourceSlotsUserBound">A dictionary that maps slots for user-bound resources onto the slots' name.</param>
	/// <param name="_resourceKeys">An array of resource keys from which to populate the resource slots. If null, keys will be
	/// used from the slots' currently assigned values.</param>
	/// <param name="_loadImmediately">Whether to load resources immediately upon assigning them. If false, they will be queued
	/// up for asynchronous loading instead.</param>
	/// <returns>True if initializing slots was successful and resources loading was initiated, false otherwise.</returns>
	protected bool InitializeBoundResourceSlots(
		IReadOnlyDictionary<string, MaterialUserBoundResourceSlot> resourceSlotsUserBound,					//TODO: This was copy-pasted from `SurfaceMaterial`; refactor or unify these functions.
		string?[]? _resourceKeys,
		bool _loadImmediately)
	{
		int i = 0;
		if (_resourceKeys is null)
		{
			_resourceKeys = new string?[resourceSlotsUserBound.Count];
			foreach (var kvp in resourceSlotsUserBound)
			{
				_resourceKeys[i++] = kvp.Value.ResourceKey;
			}
		}

		bool success = true;

		i = 0;
		foreach (var kvp in resourceSlotsUserBound)
		{
			string? resourceKey = _resourceKeys[i++];
			if (string.IsNullOrEmpty(resourceKey))
				continue;

			if (kvp.Value.resourceKind == ResourceKind.Sampler)
			{
				success &= TryLoadBoundSampler(resourceKey, kvp.Value);
			}
			else
			{
				success &= TryLoadBoundResource(resourceKey, kvp.Value, _loadImmediately);
			}
		}

		return success;
	}

	private bool TryLoadBoundSampler(string _samplerDescriptionTxt, MaterialUserBoundResourceSlot _slot)
	{
		return graphicsCore.SamplerManager.GetSampler(_samplerDescriptionTxt, out Sampler sampler) && _slot.SetValue(sampler);
	}

	private bool TryLoadBoundResource(string _resourceKey, MaterialUserBoundResourceSlot _slot, bool _loadImmediately)
	{
		if (!resourceManager.GetResource(_resourceKey, out ResourceHandle handle))
			return true;

		if (!handle.Load(_loadImmediately))
			return true;

		return _slot.SetValue(handle);
	}

	private bool RecreateResourceSets(in CameraPassContext _cameraPassCtx, ResourceSet? _resSetObject, ref ResourceSet[]? _resourceSets)
	{
		if (_resSetObject is null || _resSetObject.IsDisposed)
		{
			logger.LogError($"Cannot recreate resource sets array for compute material '{resourceKey}' using null or disposed object resource set!");
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

	public override IEnumerator<ResourceHandle> GetResourceDependencies()
	{
		// Enumerate shaders:
		yield return ComputeShaderHandle;

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

	#endregion
}
