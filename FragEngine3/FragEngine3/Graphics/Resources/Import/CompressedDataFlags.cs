namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Flags of which types of data should be compressed.
/// </summary>
[Flags]
public enum CompressedDataFlags
{
	// GEOMETRY:

	/// <summary>
	/// Compress vertex data when exporting geometry of 3D models.
	/// </summary>
	Geometry_VertexData	    = 4,
	/// <summary>
	/// Compress polygon index data when exporting geometry of 3D models.
	/// </summary>
	Geometry_IndexData      = 8,
	/// <summary>
	/// Compress weights and morph targets of blend shapes when exporting geometry of 3D models.
	/// </summary>
	Geometry_BlendData      = 16,
	/// <summary>
	/// Compress bone weights and animations when exporting geometry of 3D models.
	/// </summary>
	Geometry_AnimationData  = 32,
	//...

	/// <summary>
	/// Combination of all data flags. May be combined with '<see cref="CompressionBehaviourFlags.ForceCompression"/>'
	/// to ensure aggressive compression is used for all exports.
	/// </summary>
	ALL                     = Geometry_VertexData | Geometry_IndexData | Geometry_BlendData | Geometry_AnimationData,
}

/// <summary>
/// Flags of how data compression should be applied. Raise the '<see cref="PreferCompression"/>' flag to
/// enable compression, or '<see cref="DontUseCompression"/>' to disable it altogether.
/// </summary>
[Flags]
public enum CompressionBehaviourFlags
{

	/// <summary>
	/// Standard value if no compression is desired. This flag does nothing if combined with any other flag.
	/// </summary>
	DontUseCompression	    = 0,
	/// <summary>
	/// Flag that indicates that compression should be used if possible. No compression will be used if this
	/// flag is missing.<para/>
	/// Unless forced compression is enabled, exported data will only be compressed if it the output is
	/// smaller than the original data.
	/// </summary>
	PreferCompression       = 1,
	/// <summary>
	/// Flag that indicates that compression should always be used, even if it makes the output larger.
	/// This flag does nothing on its own and must be combined with data flags to take effect.
	/// </summary>
	ForceCompression        = 2,
	/// <summary>
	/// Flag that indicates that data should be represented in the smallest format and using the smallest
	/// data types, even if no actual compression is applied.<para/>
	/// Example: 3D models with less than 65K vertices, that are using 32-bit indices, may be optimized to
	/// use 16-bit indices instead. This would effectively cut the memory footprint of index data in half,
	/// both in RAM and in storage.
	/// </summary>
	OptimizeDataTypes       = 4,

	/// <summary>
	/// Combination of all compression behaviour flags. 
	/// </summary>
	ALL                     = PreferCompression | ForceCompression | OptimizeDataTypes,
}
