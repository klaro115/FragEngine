using System.Collections.Frozen;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;

namespace FragEngine3.Graphics.Resources.ShaderGen;

[Obsolete]
public static class ShaderGenConstants
{
	#region Fields

	public static readonly FrozenDictionary<ShaderLanguage, string> shaderLanguageFileExtensions = new Dictionary<ShaderLanguage, string>()
	{
		[ShaderLanguage.HLSL] = ".hlsl",
		[ShaderLanguage.GLSL] = ".glsl",
		[ShaderLanguage.Metal] = ".metal",
	}.ToFrozenDictionary();

	#endregion
	#region Constants

	public const string MODULAR_SURFACE_SHADER_PS_NAME_BASE = "DefaultSurface_modular_PS";

	public const string shaderGenPrefix = "ShaderGen";

	#endregion
}
