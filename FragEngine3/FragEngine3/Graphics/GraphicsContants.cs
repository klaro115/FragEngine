using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragEngine3.Graphics
{
	public static class GraphicsContants
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

		public const string SHADER_VARIANT_ENTRYPOINT_SUFFIX_EXTENDED = "Ext";
		public const string SHADER_VARIANT_ENTRYPOINT_SUFFIX_BLENDSHAPES = "Blend";
		public const string SHADER_VARIANT_ENTRYPOINT_SUFFIX_ANIMATIONS = "Anim";

		public static readonly Dictionary<MeshVertexDataFlags, string> shaderVariantsForEntryPointSuffixes = new()
		{
			[MeshVertexDataFlags.ExtendedSurfaceData] = SHADER_VARIANT_ENTRYPOINT_SUFFIX_EXTENDED,
			[MeshVertexDataFlags.BlendShapes] = SHADER_VARIANT_ENTRYPOINT_SUFFIX_BLENDSHAPES,
			[MeshVertexDataFlags.Animations] = SHADER_VARIANT_ENTRYPOINT_SUFFIX_ANIMATIONS,
			//...
		};
		public static readonly Dictionary<string, MeshVertexDataFlags> shaderEntryPointSuffixesForVariants = new()
		{
			[SHADER_VARIANT_ENTRYPOINT_SUFFIX_EXTENDED] = MeshVertexDataFlags.ExtendedSurfaceData,
			[SHADER_VARIANT_ENTRYPOINT_SUFFIX_BLENDSHAPES] = MeshVertexDataFlags.BlendShapes,
			[SHADER_VARIANT_ENTRYPOINT_SUFFIX_ANIMATIONS] = MeshVertexDataFlags.Animations,
			//...
		};

		#endregion
		#region Constants Pipelines

		public static readonly ResourceLayoutElementDescription[] DEFAULT_SURFACE_RESOURCE_LAYOUT_DESC =
		[
			CBScene.resourceLayoutElementDesc,                      // Scene constant buffer, CBScene
			CBCamera.resourceLayoutElementDesc,						// Scene constant buffer, CBCamera
			CBObject.resourceLayoutElementDesc,						// Object constant buffer, CBObject
			LightSourceData.ResourceLayoutElementDescLightBuffer,	// Light data buffer, BufLights
			LightSourceData.ResourceLayoutElementDescShadowMaps,	// Shadow map texture array, TexShadowMaps
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
}

