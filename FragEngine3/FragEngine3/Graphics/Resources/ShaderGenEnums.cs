namespace FragEngine3.Graphics.Resources;

/// <summary>
/// Enumeration of different supported sources of albedo color.
/// In a <see cref="ShaderConfig"/>, this pertains to the base albedo of the
/// surface before any color and lighting changes are applied from optional features.
/// </summary>
public enum ShaderAlbedoSource
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
public enum ShaderLightingModel
{
	Phong			= 0,	// 'P'
	BlinnPhong,				// 'BP'
	Beckmann,				// 'B'
	//...
}
