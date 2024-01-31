using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.ShaderGen.Features;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class ShaderGenerator
{
	#region Fields

	private static readonly Stack<ShaderGenContext> contextPool = new();

	#endregion
	#region Methods

	public static void ClearPooledObjects()
	{
		contextPool.Clear();
	}

	public static bool CreatePixelShader(EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		ShaderGenConfig config = ShaderGenConfig.ConfigWhiteLit;

		return CreatePixelShaderVariation(config, _platformFlags, out _outShaderCode);
	}

	public static bool CreatePixelShaderVariation(in ShaderGenConfig _config, EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		ShaderGenLanguage language;
		if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_D3D))
			language = ShaderGenLanguage.HLSL;
		else if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Metal))
			language = ShaderGenLanguage.Metal;
		else
			language = ShaderGenLanguage.GLSL;

		bool success = true;

		// Grab a free context or create a new one for this shader:
		if (!contextPool.TryPop(out ShaderGenContext? ctx))
		{
			ctx = new(language);
		}
		StringBuilder finalBuilder = new(4096);

		// Add all variants that we're already aware of:
		if (_config.alwaysCreateExtendedVariant)	ctx.variants.Add(new(MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData));
		if (_config.alwaysCreateBlendShapeVariant)	ctx.variants.Add(new(MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.BlendShapes));
		if (_config.alwaysCreateAnimatedVariant)	ctx.variants.Add(new(MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.Animations));

		// Add universal header defines and compiler instructions:
		success &= ShaderGenUtility.WriteLanguageCodeLines(finalBuilder, language,
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

		success &= ShaderGenVertexOutputs.WriteVertexOutput_Basic(in ctx);

		success &= ShaderGenAlbedo.WriteVariable_Albedo(in ctx, in _config);

		if (_config.useNormalMap)
		{
			success &= ShaderGenNormals.WriteVariable_NormalMap(in ctx, in _config);
		}

		if (_config.applyLighting)
		{
			success &= ShaderGenLighting.WriteVariable_Lighting(in ctx, in _config);
			success &= ShaderGenLighting.ApplyLighting(in ctx);
		}

		// Ensure that all required vertex output definitions are defined:
		MeshVertexDataFlags allVertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
		foreach (ShaderGenVariant variant in ctx.variants)
		{
			allVertexDataFlags |= variant.vertexDataFlags;
		}
		success &= ShaderGenVertexOutputs.WriteVertexOutput_Basic(in ctx);
		if (allVertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData))
			success &= ShaderGenVertexOutputs.WriteVertexOutput_Extended(in ctx);
		if (allVertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes))
			success &= ShaderGenVertexOutputs.WriteVertexOutput_BlendShapes(in ctx);
		if (allVertexDataFlags.HasFlag(MeshVertexDataFlags.Animations))
			success &= ShaderGenVertexOutputs.WriteVertexOutput_Animated(in ctx);

		// Assemble full shader code file:
		finalBuilder.Append(ctx.constants).AppendLine();
		finalBuilder.Append(ctx.vertexOutputs).AppendLine();
		if (language != ShaderGenLanguage.Metal)
		{
			finalBuilder.Append(ctx.resources).AppendLine();
		}
		finalBuilder.Append(ctx.functions).AppendLine();

		// Add entrypoint function:
		success &= ctx.WriteFunction_MainPixel(finalBuilder);

		// Output resulting shader code:
		_outShaderCode = finalBuilder.ToString();

		// Clear and return context to pool for later re-use:
		ctx.Clear();
		contextPool.Push(ctx);
		return success;
	}

	#endregion
}
