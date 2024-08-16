using System.Collections.Frozen;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class ShaderGenConstants
{
	#region Fields

	public static readonly FrozenDictionary<ShaderGenLanguage, string> shaderLanguageFileExtensions = new Dictionary<ShaderGenLanguage, string>()
	{
		[ShaderGenLanguage.HLSL] = ".hlsl",
		[ShaderGenLanguage.GLSL] = ".glsl",
		[ShaderGenLanguage.Metal] = ".metal",
	}.ToFrozenDictionary();

	#endregion
	#region Constants

	public const string MODULAR_SURFACE_SHADER_PS_NAME_BASE = "DefaultSurface_modular_PS";

	public const string shaderGenPrefix = "ShaderGen";

	#endregion
}
