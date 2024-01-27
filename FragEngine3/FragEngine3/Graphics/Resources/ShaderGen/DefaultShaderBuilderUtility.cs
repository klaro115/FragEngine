using FragEngine3.EngineCore;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class DefaultShaderBuilderUtility
{
	#region Methods

	public static void WriteColorValues(StringBuilder _dstBuilder, RgbaFloat _color)
	{
		_dstBuilder.Append(_color.R).Append(", ").Append(_color.G).Append(", ").Append(_color.B).Append(", ").Append(_color.A);
	}

	public static bool WriteLanguageCodeLines(StringBuilder _dstBuilder, DefaultShaderLanguage _language, string[]? _codeHLSL, string[]? _codeMetal, string[]? _codeGLSL, bool _appendAsLines = true)
	{
		string[]? codeLines = _language switch
		{
			DefaultShaderLanguage.HLSL => _codeHLSL,
			DefaultShaderLanguage.Metal => _codeMetal,
			DefaultShaderLanguage.GLSL => _codeGLSL,
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

	#endregion
}
