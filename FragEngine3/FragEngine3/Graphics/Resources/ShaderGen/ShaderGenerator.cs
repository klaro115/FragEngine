using FragEngine3.EngineCore;
using FragEngine3.Resources;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class ShaderGenerator
{
	#region Fields

	private static string templateCodePS = string.Empty;

	#endregion
	#region Constants

	public const string MODULAR_SURFACE_SHADER_PS_NAME_BASE = "DefaultSurface_modular_PS";

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
			//int indirectStartIdx = RemoveLine(codeBuilder, "#define FEATURE_LIGHT_INDIRECT", definesMaxEndIdx);
			//if (_config.indirectLightResolution > 1)
			//{
			//	codeBuilder.Insert(indirectStartIdx, $"#define FEATURE_LIGHT_INDIRECT {_config.indirectLightResolution}\n");
			//}
			if (!_config.applyLighting)
			{
				RemoveDefine(codeBuilder, "#define FEATURE_LIGHT", definesMaxEndIdx);
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

	private static int IndexOf(StringBuilder _builder, string _text, int _maxEndIdx)
	{
		int startIdx = -1;
		int maxStartIdx = _maxEndIdx - _text.Length;
		if (maxStartIdx < 0) return -1;

		for (int i = 0; i < maxStartIdx; ++i)
		{
			if (_builder[i] == _text[0])
			{
				for (int j = 1; j < _text.Length; ++j)
				{
					if (_builder[i + j] != _text[j])
						goto mismatch;
				}
				startIdx = i;
				break;
			}
		mismatch:
			;
		}
		return startIdx;
	}

	private static int RemoveLine(StringBuilder _builder, string _defineTxt, int _maxEndIdx)
	{
		int startIdx = IndexOf(_builder, _defineTxt, _maxEndIdx);
		if (startIdx >= 0)
		{
			// Find start and end of the line:
			int endIdx = startIdx + _defineTxt.Length;
			char c;
			for (; startIdx >= 0; startIdx--)
			{
				c = _builder[startIdx];
				if (char.IsControl(c))
					break;
			}
			for (; endIdx < _maxEndIdx; endIdx++)
			{
				c = _builder[endIdx];
				if (char.IsControl(c))
					break;
			}
			_builder.Remove(startIdx, endIdx - startIdx);
		}
		return startIdx;
	}

	#endregion
}
