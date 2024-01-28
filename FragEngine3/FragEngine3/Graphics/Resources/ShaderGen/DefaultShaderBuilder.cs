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

		if (_config.alwaysCreateExtendedVariant)	ctx.variants.Add(new(MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData));
		if (_config.alwaysCreateBlendShapeVariant)	ctx.variants.Add(new(MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.BlendShapes));
		if (_config.alwaysCreateAnimatedVariant)	ctx.variants.Add(new(MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.Animations));

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

		success &= DefaultShaderBuilderVertexOutputs.WriteVertexOutput_Basic(in ctx);

		success &= DefaultShaderBuilderAlbedo.WriteVariable_Albedo(in ctx, in _config);

		if (_config.useNormalMap)
		{
			success &= AddNormalMaps(in ctx, in _config);
		}

		if (_config.applyLighting)
		{
			success &= DefaultShaderBuilderLighting.WriteVariable_Lighting(in ctx, in _config);
			success &= DefaultShaderBuilderLighting.ApplyLighting(in ctx);
		}

		// Ensure that all required vertex output definitions are defined:
		MeshVertexDataFlags allVertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
		foreach (DefaultShaderBuilderVariant variant in ctx.variants)
		{
			allVertexDataFlags |= variant.vertexDataFlags;
		}
		success &= DefaultShaderBuilderVertexOutputs.WriteVertexOutput_Basic(in ctx);
		if (allVertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData))
			success &= DefaultShaderBuilderVertexOutputs.WriteVertexOutput_Extended(in ctx);
		if (allVertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes))
			success &= DefaultShaderBuilderVertexOutputs.WriteVertexOutput_BlendShapes(in ctx);
		if (allVertexDataFlags.HasFlag(MeshVertexDataFlags.Animations))
			success &= DefaultShaderBuilderVertexOutputs.WriteVertexOutput_Animated(in ctx);

		// Assemble full shader code file:
		finalBuilder.Append(ctx.constants).AppendLine();
		finalBuilder.Append(ctx.resources).AppendLine();
		finalBuilder.Append(ctx.vertexOutputs).AppendLine();
		finalBuilder.Append(ctx.functions).AppendLine();

		// Add entrypoint function:
		success &= ctx.WriteFunction_MainPixel(finalBuilder);

		// Output resulting shader code:
		_outShaderCode = finalBuilder.ToString();
		return success;
	}

	private static bool AddNormalMaps(in DefaultShaderBuilderContext _ctx, in DefaultShaderConfig _config)
	{
		//TODO
		return true;
	}

	#endregion
}
