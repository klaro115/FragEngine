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

		#endregion
		#region Constants Pipelines

		public static readonly ResourceLayoutElementDescription[] DEFAULT_SURFACE_RESOURCE_LAYOUT_DESC = new ResourceLayoutElementDescription[]
		{
			new ResourceLayoutElementDescription("TexMain", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
		};

		/// <summary>
		/// Base data for standard vertex layout, stored in primary vertex buffer (Slot 0).<para/>
		/// [Pos, Norm, Tex] => 32 byte
		/// </summary>
		public static readonly VertexLayoutDescription SURFACE_VERTEX_LAYOUT_DESC_BASIC = new(
			8 * sizeof(float),
			new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3, 0),
			new VertexElementDescription("Normal", VertexElementSemantic.Normal, VertexElementFormat.Float3, 3 * sizeof(float)),
			new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 6 * sizeof(float)));

		/// <summary>
		/// Extended data for standard vertex layout, stored in a secondary vertex buffer (Slot 1).<para/>
		/// [Tan, Tex2] => 20 byte
		/// </summary>
		public static readonly VertexLayoutDescription SURFACE_VERTEX_LAYOUT_DESC_EXT1 = new(
			5 * sizeof(float),
			new VertexElementDescription("Tangent", VertexElementSemantic.Normal, VertexElementFormat.Float3, 0),
			new VertexElementDescription("UV2", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 3 * sizeof(float)));

		/// <summary>
		/// Vertex layut description with basic static geometry data.<para/>
		/// [Pos, Norm, Tex]
		/// </summary>
		public static readonly VertexLayoutDescription[] SURFACE_VERTEX_LAYOUT_BASIC = new VertexLayoutDescription[]
		{
			SURFACE_VERTEX_LAYOUT_DESC_BASIC,	// [Pos, Norm, Tex]
		};

		/// <summary>
		/// Vertex layut description with full static geometry data.<para/>
		/// [Pos, Norm, Tex] + [Tan, Tex2]
		/// </summary>
		public static readonly VertexLayoutDescription[] SURFACE_VERTEX_LAYOUT_FULL = new VertexLayoutDescription[]
		{
			SURFACE_VERTEX_LAYOUT_DESC_BASIC,	// [Pos, Norm, Tex]
			SURFACE_VERTEX_LAYOUT_DESC_EXT1,	// [Tan, Tex2]
		};

		#endregion
	}
}

