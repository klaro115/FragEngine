using FragEngine3.EngineCore;
using FragEngine3.Resources;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class ShaderGenerator
{
	#region Constants

	public const string MODULAR_SURFACE_SHADER_PS_NAME_BASE = "DefaultSurface_modular_PS";

	#endregion
	#region Methods

	public static bool CreatePixelShader(ResourceManager _resourceManager, EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		ShaderGenConfig config = ShaderGenConfig.ConfigWhiteLit;

		return CreatePixelShaderVariation(_resourceManager, config, _platformFlags, out _outShaderCode);
	}

	public static bool CreatePixelShaderVariation(ResourceManager _resourceManager, in ShaderGenConfig _config, EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		// Identify the shader language of choice for current platform setup:
		ShaderGenLanguage language;
		if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_D3D))
			language = ShaderGenLanguage.HLSL;
		else if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Metal))
			language = ShaderGenLanguage.Metal;
		else
			language = ShaderGenLanguage.GLSL;

		// Determine name and extension for template shader code file:
		string templateFileName = language switch
		{
			ShaderGenLanguage.HLSL => $"{MODULAR_SURFACE_SHADER_PS_NAME_BASE}.hlsl",
			ShaderGenLanguage.Metal => $"{MODULAR_SURFACE_SHADER_PS_NAME_BASE}.metal",
			ShaderGenLanguage.GLSL => $"{MODULAR_SURFACE_SHADER_PS_NAME_BASE}.glsl",
			_ => string.Empty,
		};
		if (string.IsNullOrEmpty(templateFileName))
		{
			_resourceManager.engine.Logger.LogError($"Cannot load default pixel shader template code; file name could not be found! (Shader language: '{language}')");
			_outShaderCode = string.Empty;
			return false;
		}

		// Construct full file path inside core shader resource folder:
		string coreResourcePath = _resourceManager.fileGatherer.coreResourcePath ?? string.Empty;
		string coreShadersPath = Path.Combine(coreResourcePath, "shaders");
		string templateFilePath = Path.Combine(coreShadersPath, templateFileName);

		if (!File.Exists(templateFilePath))
		{
			_resourceManager.engine.Logger.LogError($"Template code file for default pixel shader could not be found! (File path: '{templateFilePath}')");
			_outShaderCode = string.Empty;
			return false;
		}

		// Read full template code file:
		string templateCodeTxt;
		try
		{
			templateCodeTxt = File.ReadAllText(templateFilePath, Encoding.ASCII);
		}
		catch (Exception ex)
		{
			_resourceManager.engine.Logger.LogException($"Failed to read template code file for default pixel shader! (File path: '{templateFilePath}')", ex);
			_outShaderCode = string.Empty;
			return false;
		}
		//^TODO: Only do this once, then cache template code for subsequent shader imports!
		
		StringBuilder codeBuilder = new(templateCodeTxt);
		int definesMaxEndIdx = Math.Min(1800, codeBuilder.Length);

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
			if (!_config.applyLighting)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT", definesMaxEndIdx);
			}
		}

		// Output updated shader code and return success:
		_outShaderCode = codeBuilder.ToString();
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

	#endregion
}
