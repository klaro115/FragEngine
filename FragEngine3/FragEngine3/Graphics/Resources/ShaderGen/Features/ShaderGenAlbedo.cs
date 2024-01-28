using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.ShaderGen.Features;

public static class ShaderGenAlbedo
{
	#region Methods

	public static bool WriteResources_TexMainAndSampler(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		bool success = true;

		// Declare main texture "TexMain":
		const string nameTexMain = "TexMain";
		bool alreadyDeclaredTexMain = _ctx.globalDeclarations.Contains(nameTexMain);

		if (!alreadyDeclaredTexMain)
		{
			success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.resources, _ctx.language,
				[ "Texture2D<half4> TexMain : register(ps, t2);" ],
				[ ", texture2d<half, access::read> TexMain [[ texture( 1 ) ]]" ],
				null,
				_ctx.language != ShaderGenLanguage.Metal);

			_ctx.globalDeclarations.Add(nameTexMain);
		}

		// Declare main texture's sampler:
		string nameSamplerMain = !string.IsNullOrEmpty(_config.samplerTexMain)
			? _config.samplerTexMain
			: "SamplerMain";
		bool alreadyDeclaredSamplerMain = _ctx.globalDeclarations.Contains(nameSamplerMain);

		if (!alreadyDeclaredSamplerMain)
		{
			success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.resources, _ctx.language,
				[ $"SamplerState {nameSamplerMain} : register(s1);" ],
				[ $", sampler {nameSamplerMain} [[ ??? ]]" ],	//TEMP
				null,
				_ctx.language != ShaderGenLanguage.Metal);

			_ctx.globalDeclarations.Add(nameSamplerMain);
		}

		return success;
	}

	public static bool WriteVariable_Albedo(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		bool success = true;

		// Ensure main texture and main sampler resources are declared:
		if (_config.albedoSource == ShaderGenAlbedoSource.SampleTexMain)
		{
			success &= WriteResources_TexMainAndSampler(in _ctx, in _config);
		}

		// Declare and initialize variable:
		foreach (ShaderGenVariant variant in _ctx.variants)
		{
			const string nameVar = "albedo";
			bool alreadyDeclared = variant.localDeclarations.Contains(nameVar);
			string nameVarUv = !string.IsNullOrEmpty(variant.varNameUVs)
				? variant.varNameUVs
				: ShaderGenVariant.DEFAULT_VAR_NAME_UVs;

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

			// Increment slot indices for material-bound resources:
			_ctx.boundTextureIdx++;
			_ctx.boundSamplerIdx++;

			if (!alreadyDeclared) variant.localDeclarations.Add(nameVar);
		}
		return success;
	}

	#endregion
}
