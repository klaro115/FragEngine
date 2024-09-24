using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using System.Collections.Frozen;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats;

public static class ShaderConstants
{
	#region Fields

	public static readonly FrozenDictionary<ShaderLanguage, string> shaderLanguageFileExtensions = new Dictionary<ShaderLanguage, string>()
	{
		[ShaderLanguage.HLSL] = ".hlsl",
		[ShaderLanguage.GLSL] = ".glsl",
		[ShaderLanguage.Metal] = ".metal",
	}.ToFrozenDictionary();

	#endregion
}
