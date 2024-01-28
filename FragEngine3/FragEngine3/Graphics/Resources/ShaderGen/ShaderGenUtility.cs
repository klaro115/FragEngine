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

	public static string SelectName(string? _customName, string _fallbackName)
	{
		return !string.IsNullOrEmpty(_customName)
			? _customName
			: _fallbackName;
	}

	#endregion
}
