using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragEngine3.Graphics;

public static class GraphicsConstants
{
	#region Constants

	public const string SETTINGS_ROOT_DIR_REL_PATH = "config/settings";

	public const string SETTINGS_FILE_NAME = "graphics.json";

	// Name suffixes required for resource keys of various shader stages:
	public const string SHADER_RESOURCE_SUFFIX_COMPUTE = "_CS";
	public const string SHADER_RESOURCE_SUFFIX_VERTEX = "_VS";
	public const string SHADER_RESOURCE_SUFFIX_GEOMETRY = "_GS";
	public const string SHADER_RESOURCE_SUFFIX_TESSELATION_CTRL = "_TS_C";
	public const string SHADER_RESOURCE_SUFFIX_TESSELATION_EVAL = "_TS_E";
	public const string SHADER_RESOURCE_SUFFIX_PIXEL = "_PS";

	public static readonly Dictionary<ShaderStages, string> shaderResourceSuffixes = new()
	{
		[ShaderStages.Compute] = SHADER_RESOURCE_SUFFIX_COMPUTE,
		[ShaderStages.Vertex] = SHADER_RESOURCE_SUFFIX_VERTEX,
		[ShaderStages.Geometry] = SHADER_RESOURCE_SUFFIX_GEOMETRY,
		[ShaderStages.TessellationControl] = SHADER_RESOURCE_SUFFIX_TESSELATION_CTRL,
		[ShaderStages.TessellationEvaluation] = SHADER_RESOURCE_SUFFIX_TESSELATION_EVAL,
		[ShaderStages.Fragment] = SHADER_RESOURCE_SUFFIX_PIXEL,
	};

	// Default entry point function names for various shader stages:
	public const string SHADER_DEFAULT_ENTRYPOINT_COMPUTE = "Main_Compute";
	public const string SHADER_DEFAULT_ENTRYPOINT_VERTEX = "Main_Vertex";
	public const string SHADER_DEFAULT_ENTRYPOINT_GEOMETRY = "Main_Geometry";
	public const string SHADER_DEFAULT_ENTRYPOINT_TESSELATION_CTRL = "Main_Tesselation_Ctrl";
	public const string SHADER_DEFAULT_ENTRYPOINT_TESSELATION_EVAL = "Main_Tesselation_Eval";
	public const string SHADER_DEFAULT_ENTRYPOINT_PIXEL = "Main_Pixel";

	public static readonly Dictionary<ShaderStages, string> defaultShaderStageEntryPoints = new()
	{
		[ShaderStages.Compute] = SHADER_DEFAULT_ENTRYPOINT_COMPUTE,
		[ShaderStages.Vertex] = SHADER_DEFAULT_ENTRYPOINT_VERTEX,
		[ShaderStages.Geometry] = SHADER_DEFAULT_ENTRYPOINT_GEOMETRY,
		[ShaderStages.TessellationControl] = SHADER_DEFAULT_ENTRYPOINT_TESSELATION_CTRL,
		[ShaderStages.TessellationEvaluation] = SHADER_DEFAULT_ENTRYPOINT_TESSELATION_EVAL,
		[ShaderStages.Fragment] = SHADER_DEFAULT_ENTRYPOINT_PIXEL,
	};

	public static readonly Dictionary<MeshVertexDataFlags, string> shaderVariantsForEntryPointSuffixes = new()
	{
		[MeshVertexDataFlags.ExtendedSurfaceData] = ExtendedVertex.shaderEntryPointSuffix,
		[MeshVertexDataFlags.BlendShapes] = IndexedWeightedVertex.shaderEntryPointSuffix_Blend,
		[MeshVertexDataFlags.Animations] = IndexedWeightedVertex.shaderEntryPointSuffix_Anim,
		//...
	};
	public static readonly Dictionary<string, MeshVertexDataFlags> shaderEntryPointSuffixesForVariants = new()
	{
		[ExtendedVertex.shaderEntryPointSuffix] = MeshVertexDataFlags.ExtendedSurfaceData,
		[IndexedWeightedVertex.shaderEntryPointSuffix_Blend] = MeshVertexDataFlags.BlendShapes,
		[IndexedWeightedVertex.shaderEntryPointSuffix_Anim] = MeshVertexDataFlags.Animations,
		//...
	};

	#endregion
	#region Constants Pipelines

	public static readonly ResourceLayoutElementDescription[] DEFAULT_CAMERA_RESOURCE_LAYOUT_DESC =
	[
		CBScene.resourceLayoutElementDesc,							// Constant buffer with scene-wide data.
		CBCamera.resourceLayoutElementDesc,							// Constant buffer with camera-specific data.
		LightConstants.ResourceLayoutElementDescBufLights,			// Structured buffer containing light data.
		LightConstants.ResourceLayoutElementDescTexShadowDepthMaps,	// Texture array containing shadow depth maps.
		LightConstants.ResourceLayoutElementDescTexShadowNormalMaps,// Texture array containing shadow normal maps.
		LightConstants.ResourceLayoutElementDescBufShadowMatrices,	// Structured buffer containing shadow projection matrices.
		LightConstants.ResourceLayoutElementDescSamplerShadowMaps,	// Sampler used for reading shadow maps.
	];
	public static readonly ResourceLayoutElementDescription[] DEFAULT_OBJECT_RESOURCE_LAYOUT_DESC =
	[
		CBObject.resourceLayoutElementDesc,							// Constant buffer with object-specific data.
	];

	/// <summary>
	/// Vertex layut description with basic static geometry data.<para/>
	/// [Pos, Norm, Tex]
	/// </summary>
	public static readonly VertexLayoutDescription[] SURFACE_VERTEX_LAYOUT_BASIC =
	[
		BasicVertex.vertexLayoutDesc,			// [Pos, Norm, Tex]
	];

	/// <summary>
	/// Vertex layut description with full static geometry data.<para/>
	/// [Pos, Norm, Tex] + [Tan, Tex2]
	/// </summary>
	public static readonly VertexLayoutDescription[] SURFACE_VERTEX_LAYOUT_EXTENDED =
	[
		BasicVertex.vertexLayoutDesc,			// [Pos, Norm, Tex]
		ExtendedVertex.vertexLayoutDesc,		// [Tan, Tex2]
	];

	/// <summary>
	/// Vertex layut description for characters with extended surface geometry data, blend shapes, and bone animations.<para/>
	/// [Pos, Norm, Tex] + [Tan, Tex2] + [BlendIndices, BlendWeight] + [BoneIndices, BoneWeights]
	/// </summary>
	public static readonly VertexLayoutDescription[] SURFACE_VERTEX_LAYOUT_CHARACTER =
	[
		BasicVertex.vertexLayoutDesc,			// [Pos, Norm, Tex]
		ExtendedVertex.vertexLayoutDesc,		// [Tan, Tex2]
		IndexedWeightedVertex.vertexLayoutDesc,	// [BlendIndices, BlendWeight]
		IndexedWeightedVertex.vertexLayoutDesc,	// [BoneIndices, BoneWeights]
	];

	#endregion
}
