namespace FragEngine3.Graphics.ConstantBuffers;

/// <summary>
/// Enumeration of constant buffer types. This differentiates only standard system-side buffers
/// explicitly, and groups all user-defined constant buffer data types as <see cref="Custom"/>.
/// </summary>
[Flags]
public enum ConstantBufferType
{
	/// <summary>
	/// A system-side constant buffer containing scene-wide information. This data is persistent
	/// across all draw calls of all renderers in a scene. Contents may change at most once per
	/// frame.
	/// </summary>
	CBScene             = 1,
	/// <summary>
	/// A system-side constant buffer containing information that is valid for all draw calls of
	/// a single camera pass. Contents may change at most once per pass and per camera.
	/// </summary>
	CBCamera            = 2,
	/// <summary>
	/// A system-side constant buffer containing object/mesh-specific information for drawing
	/// a single instance of a 3D object. Contents will usually change only once per frame.
	/// </summary>
	CBObject            = 4,
	/// <summary>
	/// A system-side constant buffer containing material-specific information for default
	/// surface materials. Contents are updated and updated automatically by the material whenever
	/// the material settings are changed, and should change at most once per camera pass.
	/// </summary>
	CBDefaultSurface    = 8,

	/// <summary>
	/// Any constant buffer type that is not provided by the engine itself. This includes all
	/// user-defined constant buffer data types.
	/// </summary>
	Custom              = 16,
}
