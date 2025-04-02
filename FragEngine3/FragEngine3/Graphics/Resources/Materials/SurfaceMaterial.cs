using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials.Internal;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

/// <summary>
/// Base class for surface materials. Material types inheriting from this class will be used to render a mesh's surface geometry.
/// </summary>
public abstract class SurfaceMaterial : Material
{
	#region Constructors

	protected SurfaceMaterial(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_graphicsCore, _resourceHandle, _data)
	{
		const int maxVariantCount = (int)MeshVertexDataFlags.ALL;
		shaderSetDescriptions = new ShaderSetDescription?[maxVariantCount];

		// Load resource handles for vertex and pixel shaders:
		if (!resourceManager.GetResource(_data.Shaders.Vertex, out ResourceHandle handleVS) ||
			!resourceManager.GetResource(_data.Shaders.Pixel, out ResourceHandle handlePS))
		{
			throw new Exception("Pixel or vertex shader of surface material could not be found!");
		}
		VertexShaderHandle = handleVS;
		PixelShaderHandle = handlePS;

		// Optionally, try loading geometry and tesselation shaders:
		if (resourceManager.GetResource(_data.Shaders.Geometry, out ResourceHandle handleGS))
		{
			GeometryShaderHandle = handleGS;
		}
		if (resourceManager.GetResource(_data.Shaders.TesselationCtrl, out ResourceHandle handleTCS) &&
			resourceManager.GetResource(_data.Shaders.TesselationEval, out ResourceHandle handleTES))
		{
			TesselationCtrlShaderHandle = handleTCS;
			TesselationEvalShaderHandle = handleTES;
		}
	}

	#endregion
	#region Fields

	protected ShaderSetDescription?[] shaderSetDescriptions;
	protected ShaderResource? vertexShader = null;
	protected ShaderResource? geometryShader = null;
	protected ShaderResource? tesselationCtrlShader = null;
	protected ShaderResource? tesselationEvalShader = null;
	protected ShaderResource? pixelShader = null;

	#endregion
	#region Properties

	/// <summary>
	/// Resource handle for the material's vertex shader. This should never be null or invalid.
	/// </summary>
	public ResourceHandle VertexShaderHandle { get; protected set; } = ResourceHandle.None;
	/// <summary>
	/// Resource handle for the material's geometry shader.
	/// </summary>
	public ResourceHandle GeometryShaderHandle { get; protected set; } = ResourceHandle.None;
	/// <summary>
	/// Resource handle for the control stage of the material's tesselation shader.
	/// </summary>
	public ResourceHandle TesselationCtrlShaderHandle { get; protected set; } = ResourceHandle.None;
	/// <summary>
	/// Resource handle for the evaluation stage of the material's tesselation shader.
	/// </summary>
	public ResourceHandle TesselationEvalShaderHandle { get; protected set; } = ResourceHandle.None;
	/// <summary>
	/// Resource handle for the material's pixel shader. This should never be null or invalid.
	/// </summary>
	public ResourceHandle PixelShaderHandle { get; protected set; } = ResourceHandle.None;

	#endregion
	#region Methods

	protected bool GetOrCreateShaderSet(MeshVertexDataFlags _vertexFlags, out ShaderSetDescription _outShaderSetDesc, out bool _outIsFullyLoaded)
	{
		uint variantIdx = _vertexFlags.GetVariantIndex();

		ShaderSetDescription? desc = shaderSetDescriptions[variantIdx];
		if (desc is not null)
		{
			_outShaderSetDesc = desc.Value;
			_outIsFullyLoaded = true;
			return true;
		}

		const bool loadImmediately = true;  //TEMP

		if (!CreateShaderSet(_vertexFlags, loadImmediately, out _outShaderSetDesc, out _outIsFullyLoaded))
		{
			return false;
		}

		shaderSetDescriptions[variantIdx] = _outShaderSetDesc;
		return true;
	}

	private bool CreateShaderSet(MeshVertexDataFlags _vertexFlags, bool _loadImmediatelyIfMissing, out ShaderSetDescription _outShaderSetDesc, out bool _outIsFullyLoaded)
	{
		if (!VertexShaderHandle.IsValid && vertexShader is null)
		{
			logger.LogError($"Cannot create shader set description for surface material '{resourceKey}' without vertex shader!");
			_outShaderSetDesc = default;
			_outIsFullyLoaded = false;
			return false;
		}
		if (!PixelShaderHandle.IsValid && pixelShader is null)
		{
			logger.LogError($"Cannot create shader set description for surface material '{resourceKey}' without pixel shader!");
			_outShaderSetDesc = default;
			_outIsFullyLoaded = false;
			return false;
		}

		// Determine how many shader stages there are:
		bool hasGeometryStage =
			GeometryShaderHandle.IsValid || geometryShader is not null;
		bool hasTesselationStage =
			(TesselationCtrlShaderHandle.IsValid || tesselationCtrlShader is not null) &&
			(TesselationEvalShaderHandle.IsValid || tesselationEvalShader is not null);

		int shaderStageCount = 2;
		if (hasGeometryStage) shaderStageCount++;
		if (hasTesselationStage) shaderStageCount += 2;

		// Create buffer for this variant's shader programs:
		Shader[] shaders = new Shader[shaderStageCount];

		// Get or load shaders for all stages:
		bool success = true;
		bool fullyLoaded = true;
		int stageIdx = 0;

		success &= GetOrLoadShader(VertexShaderHandle, ref vertexShader);
		if (hasGeometryStage)
		{
			success &= GetOrLoadShader(GeometryShaderHandle, ref geometryShader);
		}
		if (hasTesselationStage)
		{
			success &= GetOrLoadShader(TesselationCtrlShaderHandle, ref tesselationCtrlShader);
			success &= GetOrLoadShader(TesselationEvalShaderHandle, ref tesselationEvalShader);
		}
		success &= GetOrLoadShader(PixelShaderHandle, ref pixelShader);
		_outIsFullyLoaded = fullyLoaded;

		if (!success)
		{
			logger.LogError($"Failed to load shader programs for surface material '{resourceKey}' and vertex variant '{_vertexFlags}'!");
			_outShaderSetDesc = default;
			return false;
		}
		if (!_outIsFullyLoaded)
		{
			_outShaderSetDesc = default;
			return true;
		}

		// If all shader programs are fully loaded, try creating the shader set:
		try
		{
			VertexLayoutDescription[] vertexLayouts = GetVertexLayouts(_vertexFlags);

			_outShaderSetDesc = new ShaderSetDescription(vertexLayouts, shaders);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create shader set description for surface shader '{resourceKey}' and vertex variant '{_vertexFlags}'!", ex);
			_outShaderSetDesc = default;
			_outIsFullyLoaded = false;
			return false;
		}


		// Local helper method for retrieving a shader program from resource, or at least queue it up for background loading:
		bool GetOrLoadShader(ResourceHandle _handle, ref ShaderResource? _shader)
		{
			if (_shader is null || !_shader.IsLoaded)
			{
				_shader = _handle.GetResource<ShaderResource>(_loadImmediatelyIfMissing);
				if (_shader is null)
				{
					if (_loadImmediatelyIfMissing)
					{
						logger.LogWarning($"Failed to load shader '{_handle.resourceKey}' for surface material '{resourceKey}'!");
						return false;
					}
					else
					{
						fullyLoaded = false;
						return true;
					}
				}
			}

			if (!_shader.GetShaderProgram(_vertexFlags, out Shader? shaderProgram))
			{
				logger.LogWarning($"Surface shader '{_handle.resourceKey}' does not have a variant with vertex data '{_vertexFlags}'!");
				return false;
			}

			shaders[stageIdx++] = shaderProgram!;
			return true;
		}
	}

	/// <summary>
	/// Creates an array of vertex layout descriptions for a given set of vertex data flags.
	/// </summary>
	/// <param name="_vertexFlags">Bit flags for all sets of vertex data to use. At least the '<see cref="MeshVertexDataFlags.BasicSurfaceData"/>' must be set.</param>
	/// <returns>An array of vertex layout desciptions, with at least one element.</returns>
	public static VertexLayoutDescription[] GetVertexLayouts(MeshVertexDataFlags _vertexFlags)
	{
		bool hasExtData = _vertexFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
		bool hasBlendData = _vertexFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
		bool hasAnimData = _vertexFlags.HasFlag(MeshVertexDataFlags.Animations);

		int vertexDataCount = 1;
		if (hasExtData) vertexDataCount++;
		if (hasBlendData) vertexDataCount++;
		if (hasAnimData) vertexDataCount++;

		VertexLayoutDescription[] vertexLayoutDescs = new VertexLayoutDescription[vertexDataCount];

		int vertexDataIdx = 0;

		vertexLayoutDescs[vertexDataIdx++] = BasicVertex.vertexLayoutDesc;
		if (hasExtData) vertexLayoutDescs[vertexDataIdx++] = ExtendedVertex.vertexLayoutDesc;
		if (hasBlendData) vertexLayoutDescs[vertexDataIdx++] = IndexedWeightedVertex.vertexLayoutDesc;
		if (hasAnimData) vertexLayoutDescs[vertexDataIdx++] = IndexedWeightedVertex.vertexLayoutDesc;

		return vertexLayoutDescs;
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
		IReadOnlyDictionary<string, MaterialUserBoundResourceSlot> resourceSlotsUserBound,
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

	#endregion
}
