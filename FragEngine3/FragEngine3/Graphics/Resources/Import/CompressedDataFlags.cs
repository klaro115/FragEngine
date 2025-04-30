namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Flags of which types of data should be compressed.
/// </summary>
[Flags]
public enum CompressedDataFlags
{
	// GENERAL:

	DontUseCompression      = 0,

	// GEOMETRY:

	Geometry_VertexData	    = 1,
	Geometry_IndexData      = 2,
	Geometry_BlendData      = 4,
	Geometry_AnimationData  = 8,
	//...

	ALL                     = 0b1111
}
