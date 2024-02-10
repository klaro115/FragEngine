using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.ShaderGen.Features;

public static class ShaderGenAlbedo
{
	#region Methods

	public static bool WriteVariable_Albedo(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		bool success = true;

		// Ensure main texture and main sampler resources are declared:
		if (_config.albedoSource == ShaderGenAlbedoSource.SampleTexMain)
		{			
			string nameSamplerMain = ShaderGenUtility.SelectName(_config.samplerTexMain, "SamplerMain");

			success &= ShaderGenUtility.WriteResources_TextureAndSampler(in _ctx, "TexMain", 4, _ctx.boundTextureIdx, false, nameSamplerMain, _ctx.boundSamplerIdx, out bool texMainAdded, out bool samplerAdded);
			if (texMainAdded) _ctx.boundTextureIdx++;
			if (samplerAdded) _ctx.boundSamplerIdx++;
		}

		// Declare and initialize variable:
		foreach (ShaderGenVariant variant in _ctx.variants)
		{
			const string nameVar = "albedo";
			bool alreadyDeclared = variant.localDeclarations.Contains(nameVar);
			string nameVarUv = ShaderGenUtility.SelectName(variant.varNameUVs, ShaderGenVariant.DEFAULT_VAR_NAME_UVs);

			// Write basic declaration:
			variant.code.AppendLine("    // Sample base color from main texture:");
			if (alreadyDeclared)
			{
				variant.code.Append("    albedo = ");
			}
			else
			{
				variant.code.Append("    half4 albedo = ");
			}

			// Initialize vector either from color literal or sample from main texture:
			switch (_ctx.language)
			{
				case ShaderGenLanguage.HLSL:
					if (_config.albedoSource == ShaderGenAlbedoSource.SampleTexMain)
					{
						// Sample texture:
						variant.code.AppendLine($"TexMain.Sample(SamplerMain, {nameVarUv});");
					}
					else
					{
						// Color literal:
						variant.code.Append($"half4(");
						ShaderGenUtility.WriteColorValues(variant.code, _config.albedoColor);
						variant.code.AppendLine(");");
					}
					break;
				case ShaderGenLanguage.Metal:
					if (_config.albedoSource == ShaderGenAlbedoSource.SampleTexMain)
					{
						// Sample texture:
						variant.code.AppendLine($"TexMain.sample(SamplerMain, {nameVarUv});");
					}
					else
					{
						// Color literal:
						variant.code.Append($"half4(");
						ShaderGenUtility.WriteColorValues(variant.code, _config.albedoColor);
						variant.code.AppendLine(");");
					}
					break;
				default:
					{
						Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
						return false;
					}
			}
			variant.code.AppendLine();

			if (!alreadyDeclared) variant.localDeclarations.Add(nameVar);
		}
		return success;
	}

	#endregion
}
