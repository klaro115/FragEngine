namespace FragEngine3.Graphics.Resources.ShaderGen.Features;

public static class DefaultShaderBuilderVertexOutputs
{
	#region Constants

	public const string NAME_VERTEX_OUTPUT_BASIC = "VertexOutput_Basic";
	public const string NAME_VERTEX_OUTPUT_EXT = "VertexOutput_Extended";
	public const string NAME_VERTEX_OUTPUT_BLEND = "VertexOutput_Blend";
	public const string NAME_VERTEX_OUTPUT_ANIM = "VertexOutput_Anim";

	#endregion
	#region Methods

	public static bool WriteVertexOutput_Basic(in DefaultShaderBuilderContext _ctx)
	{
		if (_ctx.globalDeclarations.Contains(NAME_VERTEX_OUTPUT_BASIC)) return true;

		bool success = true;

		// Write structure header:
		_ctx.vertexOutputs
			.AppendLine("struct VertexOutput_Basic")
			.AppendLine("{");

		// Write body:
		success &= DefaultShaderBuilderUtility.WriteLanguageCodeLines(_ctx.vertexOutputs, _ctx.language,
			// HLSL:
			[
				"    float4 position : SV_POSITION;",
				"    float3 worldPosition : COLOR0;",
				"    float3 normal : NORMAL0;",
				"    float2 uv : TEXCOORD0;"
			],
			// Metal:
			[
				"    float4 position; [[ position ]]",
				"    float3 worldPosition;",
				"    float3 normal;",
				"    float2 uv;"
			],
			// GLSL:
			null);
		_ctx.vertexOutputs.AppendLine("};");

		_ctx.globalDeclarations.Add(NAME_VERTEX_OUTPUT_BASIC);
		return success;
	}

	public static bool WriteVertexOutput_Extended(in DefaultShaderBuilderContext _ctx)
	{
		if (_ctx.globalDeclarations.Contains(NAME_VERTEX_OUTPUT_EXT)) return true;

		bool success = true;

		// Write structure header:
		_ctx.vertexOutputs
			.AppendLine("struct VertexOutput_Extended")
			.AppendLine("{");

		// Write body:
		success &= DefaultShaderBuilderUtility.WriteLanguageCodeLines(_ctx.vertexOutputs, _ctx.language,
			// HLSL:
			[
				"    float4 tangent : NORMAL1;",
				"    float3 binormal : NORMAL2;",
				"    float2 uv2 : TEXCOORD1;"
			],
			// Metal:
			[
				"    float4 tangent;",
				"    float3 binormal;",
				"    float2 uv2;"
			],
			// GLSL:
			null);
		_ctx.vertexOutputs.AppendLine("};");

		_ctx.globalDeclarations.Add(NAME_VERTEX_OUTPUT_EXT);
		return success;
	}

	public static bool WriteVertexOutput_BlendShapes(in DefaultShaderBuilderContext _ctx)
	{
		if (_ctx.globalDeclarations.Contains(NAME_VERTEX_OUTPUT_BLEND)) return true;

		bool success = true;

		// Write structure header:
		_ctx.vertexOutputs
			.AppendLine("struct VertexOutput_Blend")
			.AppendLine("{");

		// Write body:
		success &= DefaultShaderBuilderUtility.WriteLanguageCodeLines(_ctx.vertexOutputs, _ctx.language,
			// HLSL:
			[
				"    uint4 indices : NORMAL3;",
				"    float3 weights : TEXCOORD2;",
			],
			// Metal:
			[
				"    uint4 indices;",
				"    float3 weights;",
			],
			// GLSL:
			null);
		_ctx.vertexOutputs.AppendLine("};");

		_ctx.globalDeclarations.Add(NAME_VERTEX_OUTPUT_BLEND);
		return success;
	}

	public static bool WriteVertexOutput_Animated(in DefaultShaderBuilderContext _ctx)
	{
		if (_ctx.globalDeclarations.Contains(NAME_VERTEX_OUTPUT_ANIM)) return true;

		bool success = true;

		// Write structure header:
		_ctx.vertexOutputs
			.AppendLine("struct VertexOutput_Anim")
			.AppendLine("{");

		// Write body:
		success &= DefaultShaderBuilderUtility.WriteLanguageCodeLines(_ctx.vertexOutputs, _ctx.language,
			// HLSL:
			[
				"    uint4 indices : NORMAL4;",
				"    float3 weights : TEXCOORD3;",
			],
			// Metal:
			[
				"    uint4 indices;",
				"    float3 weights;",
			],
			// GLSL:
			null);
		_ctx.vertexOutputs.AppendLine("};");

		_ctx.globalDeclarations.Add(NAME_VERTEX_OUTPUT_ANIM);
		return success;
	}

	#endregion
}
