using FragEngine3.EngineCore;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public sealed class ShaderGenCodeDeclaration
{
	#region Properties

	/// <summary>
	/// A unique name for this declaration, which is used as an identifier key. No two global declarations can use the same name.
	/// </summary>
	public string Name { get; set; } = string.Empty;
	/// <summary>
	/// A code block that shall be added at the start of a shader code file when this declaration is added. This could be a define
	/// instruction, a global constant (static const, constexpr), or a resource declaration (textures, buffers, uniforms). This code
	/// may be templated, i.e. it has placeholder names in it that will be replaced by contextual information before it is added to
	/// a shader code file.
	/// </summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>
	/// [Optional] An array of named templates that represent code that needs to be replaced or completed before addition to a code file.
	/// It is recommended that all template names listed herein and used throughout the source code should start with a '$' sign, followed
	/// by a unique descriptive name in all caps without spaces.<para/>
	/// EXAMPLES: "$NORMAL_MAP_NAME", or "$MAIN_COLOR", or "$"NUM_LIGHTS""
	/// </summary>
	public string[]? TemplateNames { get; set; } = null;

	#endregion
	#region Methods

	public bool IsCodeTemplated() => TemplateNames != null && TemplateNames.Length != 0;

	public bool CreateCode(StringBuilder _dstBuilder, StringBuilder _templateBuilder, IList<string> _templateReplacements)
	{
		if (_dstBuilder == null)
		{
			Logger.Instance?.LogError($"Cannot create declaration code for '{Name ?? "NULL"}' using null string builder!");
			return false;
		}
		if (string.IsNullOrEmpty(Code))
		{
			Logger.Instance?.LogWarning($"Code declaration '{Name ?? "NULL"}' adds no shader code and can be omitted.");
			return true;
		}

		// If code is not templated, add it as-is:
		if (!IsCodeTemplated())
		{
			_dstBuilder.Append(Code);
			return true;
		}

		// Apply template replacements in source code:
		if (!ShaderGenTemplateUtility.CreateCodeFromTemplate(Code, TemplateNames!, _templateBuilder, _templateReplacements))
		{
			Logger.Instance?.LogError($"Failed to create declaration code for '{Name ?? "NULL"}'!");
			return false;
		}

		// Add final code to main code block:
		_dstBuilder.Append(_templateBuilder);
		return true;
	}

	#endregion
}
