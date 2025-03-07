using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

public abstract class SurfaceMaterial : MaterialNew
{
	#region Constructors

	protected SurfaceMaterial(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_graphicsCore, _resourceHandle, _data)
	{
		const int maxVariantCount = (int)MeshVertexDataFlags.ALL;
		shaderSetDescriptions = new ShaderSetDescription?[maxVariantCount];
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

	public ResourceHandle VertexShaderHandle { get; protected set; } = ResourceHandle.None;
	public ResourceHandle GeometryShaderHandle { get; protected set; } = ResourceHandle.None;
	public ResourceHandle TesselationCtrlShaderHandle { get; protected set; } = ResourceHandle.None;
	public ResourceHandle TesselationEvalShaderHandle { get; protected set; } = ResourceHandle.None;
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

	protected static VertexLayoutDescription[] GetVertexLayouts(MeshVertexDataFlags _vertexFlags)
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

	#endregion
}
