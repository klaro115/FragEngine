using Veldrid;

namespace FragEngine3.Graphics.Lighting;

public static class LightConstants
{
	#region Constants

	public const uint SHADOW_RESOLUTION = 1024;
	public const float directionalLightSize = 10;       //TEMP

	public const float MIN_LIGHT_INTENSITY = 0.001f;

	public const float DEG2RAD = MathF.PI / 180.0f;
	public const float RAD2DEG = 180.0f / MathF.PI;

	#endregion
	#region Resource Layouts

	public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescBufLights = new("BufLights", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment);
	public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescTexShadowDepthMaps = new("TexShadowMaps", ResourceKind.TextureReadOnly, ShaderStages.Fragment);
	public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescTexShadowNormalMaps = new("TexShadowNormalMaps", ResourceKind.TextureReadOnly, ShaderStages.Fragment);
	public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescBufShadowMatrices = new("BufShadowMatrices", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment);
	public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescSamplerShadowMaps = new("SamplerShadowMaps", ResourceKind.Sampler, ShaderStages.Fragment);

	#endregion
}
