using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.ShaderGen.Features;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class DefaultShaderBuilder
{
	#region Methods

	public static bool CreatePixelShader(EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		DefaultShaderConfig config = DefaultShaderConfig.ConfigWhiteLit;

		return CreatePixelShaderVariation(config, _platformFlags, out _outShaderCode);
	}

	public static bool CreatePixelShaderVariation(in DefaultShaderConfig _config, EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		DefaultShaderLanguage language;
		if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_D3D))
			language = DefaultShaderLanguage.HLSL;
		else if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Metal))
			language = DefaultShaderLanguage.Metal;
		else
			language = DefaultShaderLanguage.GLSL;

		bool success = true;

		DefaultShaderBuilderContext ctx = new(language);
		StringBuilder finalBuilder = new(4096);

		// Add universal header defines and compiler instructions:
		success &= DefaultShaderBuilderUtility.WriteLanguageCodeLines(finalBuilder, language,
			// HLSL:
			[
				"#pragma pack_matrix( column_major )"
			],
			// Metal:
			[
				"#include <metal_stdlib>",
				"using namespace metal;",
			],
			[]);
		finalBuilder.AppendLine();

		success &= AddAlbedo(in ctx, in _config);

		if (_config.useNormalMap)
		{
			success &= AddNormalMaps(in ctx, in _config);
		}

		if (_config.applyLighting)
		{
			success &= DefaultShaderBuilderLighting.WriteVariable_Lighting(in ctx, in _config);
			success &= DefaultShaderBuilderLighting.ApplyLighting(in ctx);
		}

		// Assemble full shader code file:
		success &= AddHeader_MainPixel(ctx, ctx.mainHeader);
		finalBuilder.Append(ctx.constants).AppendLine();
		finalBuilder.Append(ctx.resources).AppendLine();
		finalBuilder.Append(ctx.vertexOutputs).AppendLine();
		finalBuilder.Append(ctx.functions).AppendLine();

		// Add entrypoint function:
		finalBuilder.Append(ctx.mainHeader);
		finalBuilder.Append(ctx.mainCode);
		success &= AddFunctionEnd_MainPixel(finalBuilder);

		// Output resulting shader code:
		_outShaderCode = finalBuilder.ToString();
		return success;
	}

	private static bool WriteVertexOutput_Basic(in DefaultShaderBuilderContext _ctx)
	{
		const string nameVert = "VertexOutput_Basic";
		if (_ctx.globalDeclarations.Contains(nameVert)) return true;

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

		_ctx.globalDeclarations.Add(nameVert);
		return success;
	}

	private static bool WriteVertexOutput_Extended(in DefaultShaderBuilderContext _ctx)
	{
		const string nameVert = "VertexOutput_Extended";
		if (_ctx.globalDeclarations.Contains(nameVert)) return true;

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

		_ctx.globalDeclarations.Add(nameVert);
		return success;
	}

	private static bool AddAlbedo(in DefaultShaderBuilderContext _ctx, in DefaultShaderConfig _config)
	{
		// Write basic declaration:
		_ctx.mainCode.AppendLine("    // Sample base color from main texture:");
		_ctx.mainCode.Append("    half4 albedo = ");

		// Initialize vector either from color literal or sample from main texture:
		switch (_ctx.language)
		{
			case DefaultShaderLanguage.HLSL:
				if (_config.albedoSource == DefaultShaderAlbedoSource.SampleTexMain)
				{
					// Declare resources TexMain and its sampler:
					string samplerMainName = !string.IsNullOrEmpty(_config.samplerTexMain)
						? _config.samplerTexMain
						: "SamplerMain";
					_ctx.resources.AppendLine("Texture2D<half4> TexMain : register(ps, t2);");
					_ctx.resources.AppendLine($"SamplerState {samplerMainName} : register(s1);");

					// Sample texture:
					_ctx.mainCode.AppendLine("TexMain.Sample(SamplerMain, inputBasic.uv);");

					// Increment slot indices for material-bound resources:
					_ctx.boundTextureIdx++;
					_ctx.boundSamplerIdx++;
				}
				else
				{
					_ctx.mainCode.Append($"half4(");
					DefaultShaderBuilderUtility.WriteColorValues(_ctx.mainCode, _config.albedoColor);
					_ctx.mainCode.AppendLine(");");
				}
				break;
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
					return false;
				}
		}

		_ctx.mainCode.AppendLine();
		return true;
	}

	private static bool AddNormalMaps(in DefaultShaderBuilderContext _ctx, in DefaultShaderConfig _config)
	{
		//TODO
		return true;
	}

	private static bool AddHeader_MainPixel(in DefaultShaderBuilderContext _ctx, StringBuilder _finalBuilder)
	{
		bool success = true;

		_finalBuilder
			.AppendLine("/******************* SHADERS: ******************/")
			.AppendLine();

		bool hasExtendedData = _ctx.vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
		bool hasBlendShapes = _ctx.vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
		bool hasBoneAnimation = _ctx.vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations);

		// Ensure the required vertex output types are defined:
		success &= WriteVertexOutput_Basic(in _ctx);
		if (hasExtendedData) WriteVertexOutput_Extended(in _ctx);

		// Write function's base name:
		success &= DefaultShaderBuilderUtility.WriteLanguageCodeLines(_finalBuilder, _ctx.language,
			[ "half4 Main_Pixel(" ],
			[ "half4 fragment Main_Pixel(" ],
			null,
			false);

		// Add vertex flags to name:
		if (hasExtendedData) _finalBuilder.Append('_').Append(ExtendedVertex.shaderEntryPointSuffix);
		if (hasBlendShapes) _finalBuilder.Append('_').Append(IndexedWeightedVertex.shaderEntryPointSuffix_Blend);
		if (hasBoneAnimation) _finalBuilder.Append('_').Append(IndexedWeightedVertex.shaderEntryPointSuffix_Anim);

		// List vertex input paramaters:
		switch (_ctx.language)
		{
			case DefaultShaderLanguage.HLSL:
				{
					_finalBuilder.Append($"in {BasicVertex.shaderVertexOuputName} inputBasic");

					if (hasExtendedData) _finalBuilder.Append($", in {ExtendedVertex.shaderVertexOuputName} inputExt");
					if (hasBlendShapes) _finalBuilder.Append($", in {IndexedWeightedVertex.shaderVertexOuputName_Blend} inputBlend");
					if (hasBoneAnimation) _finalBuilder.Append($", in {IndexedWeightedVertex.shaderVertexOuputName_Anim} inputAnim");
				}
				break;
			case DefaultShaderLanguage.Metal:
				{
					_finalBuilder.Append($"{BasicVertex.shaderVertexOuputName} inputBasic [[ stage_in ]]");

					if (hasExtendedData) _finalBuilder.Append($", {ExtendedVertex.shaderVertexOuputName} inputExt");
					if (hasBlendShapes) _finalBuilder.Append($", {IndexedWeightedVertex.shaderVertexOuputName_Blend} inputBlend");
					if (hasBoneAnimation) _finalBuilder.Append($", {IndexedWeightedVertex.shaderVertexOuputName_Anim} inputAnim");
				}
				break;
			//...
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
					return false;
				}
		}

		// List any further input paramaters from other features: (mostly needed for Metal)
		if (_ctx.mainInputs.Length != 0)
		{
			_finalBuilder.Append(", ").Append(_ctx.mainInputs);
		}

		// Close function header:
		switch (_ctx.language)
		{
			case DefaultShaderLanguage.HLSL:
				{
					_finalBuilder.AppendLine(") : SV_Target0").AppendLine("{");
				}
				break;
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
					return false;
				}
		}
		return success;
	}

	private static bool AddFunctionEnd_MainPixel(StringBuilder _finalBuilder)
	{
		_finalBuilder.AppendLine(
			"    // Return final color:\n" +
			"    return albedo;\n" +
			"};");
		return true;
	}

	#endregion
}
