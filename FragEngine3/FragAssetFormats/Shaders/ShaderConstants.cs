using FragEngine3.Graphics.Resources.Shaders;
using System.Collections.Frozen;

namespace FragAssetFormats.Shaders;

[Obsolete("might no longer be used")]
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
