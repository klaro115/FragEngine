namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Flags of which types of data should be compressed.
/// </summary>
[Flags]
public enum CompressedDataFlags
{
	// GENERAL:

	/// <summary>
	/// Standard value if no compression is desired. This flag does nothing if combined with any other flag.
	/// </summary>
	DontUseCompression      = 0,
	/// <summary>
	/// Flag that indicates that compression should always be used, even if it makes the output larger.
	/// This föag does nothing on its own and must be combined with other flags to take effect.
	/// </summary>
	ForceCompression        = 1,

	// GEOMETRY:

	/// <summary>
	/// Compress vertex data when exporting geometry of 3D models.
	/// </summary>
	Geometry_VertexData	    = 2,
	/// <summary>
	/// Compress polygon index data when exporting geometry of 3D models.
	/// </summary>
	Geometry_IndexData      = 4,
	/// <summary>
	/// Compress weights and morph targets of blend shapes when exporting geometry of 3D models.
	/// </summary>
	Geometry_BlendData      = 8,
	/// <summary>
	/// Compress bone weights and animations when exporting geometry of 3D models.
	/// </summary>
	Geometry_AnimationData  = 16,
	//...

	/// <summary>
	/// Combination of all data flags. May be combined with '<see cref="ForceCompression"/>' to ensure
	/// aggressive compression is used for all exports.
	/// </summary>
	ALL                     = 0b11110
}
