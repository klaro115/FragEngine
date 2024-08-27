using FragEngine3.EngineCore;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources;
using FragEngine3.Utility;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class ShaderGenerator
{
	#region Fields

	private static string templateCodePS = string.Empty;

	#endregion
	#region Methods

	public static bool CreatePixelShader(ResourceManager _resourceManager, EnginePlatformFlag _platformFlags, out byte[] _outShaderCode)
	{
		ShaderGenConfig config = ShaderGenConfig.ConfigWhiteLit;

		return CreatePixelShaderVariation(_resourceManager, config, _platformFlags, out _outShaderCode);
	}

	public static bool CreatePixelShaderVariation(ResourceManager _resourceManager, in ShaderGenConfig _config, EnginePlatformFlag _platformFlags, out byte[] _outShaderCode)
	{
		// Only load template code file once, cached code is used for all subsequent shader imports:
		if (string.IsNullOrEmpty(templateCodePS))
		{
			// Identify the shader language of choice for current platform setup:
			ShaderLanguage language;
			if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_D3D))
				language = ShaderLanguage.HLSL;
			else if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Metal))
				language = ShaderLanguage.Metal;
			else
				language = ShaderLanguage.GLSL;

			// Determine name and extension for template shader code file:
			string templateFileName = language switch
			{
				ShaderLanguage.HLSL => $"{ShaderGenConstants.MODULAR_SURFACE_SHADER_PS_NAME_BASE}.hlsl",
				ShaderLanguage.Metal => $"{ShaderGenConstants.MODULAR_SURFACE_SHADER_PS_NAME_BASE}.metal",
				ShaderLanguage.GLSL => $"{ShaderGenConstants.MODULAR_SURFACE_SHADER_PS_NAME_BASE}.glsl",
				_ => string.Empty,
			};
			if (string.IsNullOrEmpty(templateFileName))
			{
				_resourceManager.engine.Logger.LogError($"Cannot load default pixel shader template code; file name could not be found! (Shader language: '{language}')");
				_outShaderCode = [];
				return false;
			}

			// Construct full file path inside core shader resource folder:
			string coreResourcePath = _resourceManager.fileGatherer.coreResourcePath ?? string.Empty;
			string coreShadersPath = Path.Combine(coreResourcePath, "shaders");
			string templateFilePath = Path.Combine(coreShadersPath, templateFileName);

			if (!File.Exists(templateFilePath))
			{
				_resourceManager.engine.Logger.LogError($"Template code file for default pixel shader could not be found! (File path: '{templateFilePath}')");
				_outShaderCode = [];
				return false;
			}

			// Read full template code file:
			try
			{
				templateCodePS = File.ReadAllText(templateFilePath, Encoding.ASCII);
			}
			catch (Exception ex)
			{
				_resourceManager.engine.Logger.LogException($"Failed to read template code file for default pixel shader! (File path: '{templateFilePath}')", ex);
				_outShaderCode = [];
				return false;
			}
		}
		
		StringBuilder codeBuilder = new(templateCodePS);
		int definesMaxEndIdx = Math.Min(2100, codeBuilder.Length);

		// Drop all flags in config that are based on other feature flags that are disabled:
		_config.PropagateFlagStatesToHierarchy();

		// Remove and update preprocessor defines of features that are not needed:
		{
			// Albedo:
			if (_config.albedoSource != ShaderGenAlbedoSource.SampleTexMain)
			{
				ReplaceDefine(
					codeBuilder,
					"#define FEATURE_ALBEDO_TEXTURE 1",
					"#define FEATURE_ALBEDO_TEXTURE 0",
					definesMaxEndIdx);
			}
			if (_config.albedoSource == ShaderGenAlbedoSource.Color && _config.albedoColor != RgbaFloat.White)
			{
				ReplaceDefine(
					codeBuilder,
					"#define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)",
					$"#define FEATURE_ALBEDO_COLOR half4({_config.albedoColor.R:0.###}, {_config.albedoColor.G:0.###}, {_config.albedoColor.B:0.###}, {_config.albedoColor.A:0.###})",
					definesMaxEndIdx);
			}

			// Normals:
			if (!_config.useNormalMap)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_NORMALS", definesMaxEndIdx);
			}
			if (!_config.useParallaxMap || !_config.useParallaxMapFull)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_PARALLAX_FULL", definesMaxEndIdx);
			}
			if (!_config.useParallaxMap)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_PARALLAX", definesMaxEndIdx);
			}

			// Lighting:
			if (!_config.useAmbientLight)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT_AMBIENT", definesMaxEndIdx);
			}
			if (!_config.useLightMaps)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT_LIGHTMAPS", definesMaxEndIdx);
			}
			if (!_config.useLightSources)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT_SOURCES", definesMaxEndIdx);
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT_MODEL Phong", definesMaxEndIdx);
			}
			if (!_config.useShadowMaps)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT_SHADOWMAPS", definesMaxEndIdx);
			}
			if (_config.useLightSources && _config.lightingModel != ShaderGenLightingModel.Phong)
			{
				ReplaceDefine(
					codeBuilder,
					"#define FEATURE_LIGHT_MODEL Phong",
					$"#define FEATURE_LIGHT_MODEL {_config.lightingModel}",
					definesMaxEndIdx);
			}
			ReplaceDefineValue(
				codeBuilder,
				"#define FEATURE_LIGHT_SHADOWMAPS_RES",
				$"#define FEATURE_LIGHT_SHADOWMAPS_RES {LightConstants.SHADOW_RESOLUTION}",
				_config.useShadowMaps);
			
			ReplaceDefineValue(
				codeBuilder,
				"#define FEATURE_LIGHT_SHADOWMAPS_AA",
				$"#define FEATURE_LIGHT_SHADOWMAPS_AA {_config.shadowSamplingCount}",
				_config.useShadowMaps && _config.shadowSamplingCount > 1);

			ReplaceDefineValue(
				codeBuilder,
				"#define FEATURE_LIGHT_INDIRECT",
				$"#define FEATURE_LIGHT_INDIRECT {_config.indirectLightResolution}",
				_config.indirectLightResolution > 1);

			if (!_config.applyLighting)
			{
				codeBuilder.RemoveAllLines("#define FEATURE_LIGHT");
			}
		}

		// Output updated shader code and return success:
		_outShaderCode = new byte[codeBuilder.Length + 1];
		for (int i = 0; i < codeBuilder.Length; ++i)
		{
			_outShaderCode[i] = (byte)codeBuilder[i];
		}
		_outShaderCode[^1] = (byte)'\0';
		return true;
	}

	private static void ReplaceDefine(StringBuilder _builder, string _oldDefineTxt, string _newDefineTxt, int _maxEndIdx)
	{
		_builder.Replace(_oldDefineTxt, _newDefineTxt, 0, _maxEndIdx);
	}

	private static void RemoveDefine(StringBuilder _builder, string _defineTxt, int _maxEndIdx)
	{
		_builder.Replace(_defineTxt, string.Empty, 0, _maxEndIdx);
	}

	private static void ReplaceDefineValue(StringBuilder _builder, string _baseDefineTxt, string _newDefineTxt, bool _insertNewValue)
	{
		Range defineLineIndices = _builder.GetFirstLineIndicesOf(_baseDefineTxt);
		if (defineLineIndices.Start.Value >= 0)
		{
			_builder.Remove(defineLineIndices);
			if (_insertNewValue)
			{
				_builder.Insert(defineLineIndices.Start.Value, _newDefineTxt);
			}
		}
	}

	#endregion
}
