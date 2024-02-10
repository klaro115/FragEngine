using FragEngine3.EngineCore;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class ShaderGenUtility
{
	#region Methods

	public static void WriteColorValues(StringBuilder _dstBuilder, RgbaFloat _color)
	{
		_dstBuilder.Append(_color.R).Append(", ").Append(_color.G).Append(", ").Append(_color.B).Append(", ").Append(_color.A);
	}

	public static bool WriteLanguageCodeLines(StringBuilder _dstBuilder, ShaderGenLanguage _language, string[]? _codeHLSL, string[]? _codeMetal, string[]? _codeGLSL, bool _appendAsLines = true)
	{
		string[]? codeLines = _language switch
		{
			ShaderGenLanguage.HLSL => _codeHLSL,
			ShaderGenLanguage.Metal => _codeMetal,
			ShaderGenLanguage.GLSL => _codeGLSL,
			_ => null,
		};

		if (codeLines != null)
		{
			foreach (string line in codeLines)
			{
				_dstBuilder.Append(line);
				if (_appendAsLines)
				{
					_dstBuilder.AppendLine();
				}
			}
			return true;
		}
		Logger.Instance?.LogError($"Code lines not currently available for shading language '{_language}'.");
		return false;
	}

	public static bool WriteLanguageCodeLine(StringBuilder _dstBuilder, ShaderGenLanguage _language, string? _codeHLSL, string? _codeMetal, string? _codeGLSL, bool _appendAsLines = true)
	{
		string? codeLine = _language switch
		{
			ShaderGenLanguage.HLSL => _codeHLSL,
			ShaderGenLanguage.Metal => _codeMetal,
			ShaderGenLanguage.GLSL => _codeGLSL,
			_ => null,
		};

		if (codeLine != null)
		{
			if (_appendAsLines)
			{
				_dstBuilder.AppendLine(codeLine);
			}
			else
			{
				_dstBuilder.Append(codeLine);
			}
			return true;
		}
		Logger.Instance?.LogError($"Code line not currently available for shading language '{_language}'.");
		return false;
	}

	public static string SelectName(string? _customName, string _fallbackName)
	{
		return !string.IsNullOrEmpty(_customName)
			? _customName
			: _fallbackName;
	}

	public static bool WriteResources_TextureAndSampler(in ShaderGenContext _ctx, string _nameTex, uint _texChannelCount, uint _texSlotIdx, bool _isTextureArray, string _nameSampler, uint _samplerSlotIdx, out bool _outTexAdded, out bool _outSamplerAdded)
	{
		_outTexAdded = false;
		_outSamplerAdded = false;
		if (string.IsNullOrEmpty(_nameTex) || string.IsNullOrEmpty(_nameSampler))
		{
			Logger.Instance?.LogError($"Cannot declare texture or sampler from null or empty resource names.");
			return false;
		}

		// Texture:
		if (!_ctx.globalDeclarations.Contains(_nameTex))
		{
			_texChannelCount = Math.Clamp(_texChannelCount, 1, 4);

			// Metal:
			if (_ctx.language == ShaderGenLanguage.Metal)
			{
				foreach (ShaderGenVariant variant in _ctx.variants)
				{
					if (variant.arguments.Length != 0)
					{
						variant.arguments.Append(", ");
					}

					if (_isTextureArray)
					{
						variant.arguments.Append("texture2d_array<half, access::sample> ");
					}
					else
					{
						variant.arguments.Append("texture2d<half, access::sample> ");
					}
					variant.arguments.Append(_nameTex).Append(" [[ texture( ").Append(_texSlotIdx).AppendLine(" ) ]];");
				}
			}
			// HLSL & GLSL:
			else
			{
				if (_isTextureArray)
				{
					_ctx.resources.Append("Texture2DArray<half").Append(_texChannelCount).Append("> ").Append(_nameTex).Append(" : register(ps, t").Append(_texSlotIdx).AppendLine(");");
				}
				else
				{
					_ctx.resources.Append("Texture2D<half").Append(_texChannelCount).Append("> ").Append(_nameTex).Append(" : register(ps, t").Append(_texSlotIdx).AppendLine(");");
				}
			}
			_ctx.globalDeclarations.Add(_nameTex);
			_outTexAdded = true;
		}

		// Sampler:
		if (!_ctx.globalDeclarations.Contains(_nameSampler))
		{
			// Metal:
			if (_ctx.language == ShaderGenLanguage.Metal)
			{
				foreach (ShaderGenVariant variant in _ctx.variants)
				{
					if (variant.arguments.Length != 0)
					{
						variant.arguments.Append(", ");
					}
					variant.arguments.Append("sampler ").Append(_nameSampler).Append(" [[ sampler( ").Append(_samplerSlotIdx).AppendLine(" ) ]];");
				}
			}
			// HLSL & GLSL:
			else
			{
				_ctx.resources.Append("SamplerState ").Append(_nameSampler).Append(" : register(ps, s").Append(_samplerSlotIdx).AppendLine(");");
			}
			_ctx.globalDeclarations.Add(_nameSampler);
			_outSamplerAdded = true;
		}

		return true;
	}

	#endregion
}
