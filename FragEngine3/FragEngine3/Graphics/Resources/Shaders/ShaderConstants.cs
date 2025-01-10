using System.Collections.Frozen;

namespace FragEngine3.Graphics.Resources.Shaders;

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
