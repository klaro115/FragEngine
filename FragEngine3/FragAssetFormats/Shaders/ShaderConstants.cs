using FragAssetFormats.Shaders.ShaderTypes;
using System.Collections.Frozen;

namespace FragAssetFormats.Shaders;

public static class ShaderConstants
{
	#region Fields

	public static readonly FrozenDictionary<ShaderLanguage, string> shaderLanguageFileExtensions = new Dictionary<ShaderLanguage, string>()
	{
		[ShaderLanguage.HLSL] = ".hlsl",
		[ShaderLanguage.GLSL] = ".glsl",
		[ShaderLanguage.Metal] = ".metal",
		[ShaderLanguage.SPIRV] = ".spv",
	}.ToFrozenDictionary();

	#endregion
}
