namespace FragEngine3.Graphics.Resources.ShaderGen;

/// <summary>
/// Enumeration of different supported sources of albedo color.
/// In a <see cref="ShaderGenConfig"/>, this pertains to the base albedo of the
/// surface before any color and lighting changes are applied from optional features.
/// </summary>
public enum ShaderGenAlbedoSource
{
	/// <summary>
	/// The base albedo is assigned from a single flat color.
	/// </summary>
	Color			= 0,
	/// <summary>
	/// The base albedo is sampled from a main color texture.
	/// </summary>
	SampleTexMain,
}

/// <summary>
/// Different main lighting models supported by standard shaders.
/// Enum values are sorted in ascending order of the lighting models' relative complexity.
/// </summary>
public enum ShaderGenLightingModel
{
	Phong			= 0,
	//...
}

/// <summary>
/// Enumeration of different high level shading languages. Note that most of
/// these languages may be specific to a single platform or graphics API.
/// </summary>
[Flags]
public enum ShaderGenLanguage
{
	/// <summary>
	/// Mircosoft's Direct3D shading language. This is the default language that
	/// will be favored by the engine's systems.
	/// </summary>
	HLSL			= 1,
	/// <summary>
	/// Apple's Metal shading language (MSL), which is only supported by MacOS and
	/// iOS platforms running a Metal graphics backend.
	/// </summary>
	Metal			= 2,
	/// <summary>
	/// OpenGL shading language, which is supported by multiple graphics backends
	/// to varying degrees, including but not limited to OpenGL, GLES, and Vulkan.
	/// If HLSL source code is not available, this may be used to compile shaders
	/// for Vulkan.
	/// </summary>
	GLSL			= 4,

	/// <summary>
	/// All shader language flags are raised. This value should only be used by
	/// methods that actually expect flags rather than a singular language selection.
	/// </summary>
	ALL				= HLSL | Metal | GLSL
}
