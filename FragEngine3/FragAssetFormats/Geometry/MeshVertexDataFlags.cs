namespace FragAssetFormats.Geometry;

/// <summary>
/// Flags describing which vertex data is present or must be present for a mesh.
/// </summary>
[Flags]
public enum MeshVertexDataFlags : byte
{
	/// <summary>
	/// Basic vertex data, includes position, normals, and texture coordinates.<para/>
	/// NOTE: This must always be the first vertex buffer that is bound for standard surface meshes.
	/// Data must match the type 'BasicVertex'.
	/// </summary>
	BasicSurfaceData = 1,
	/// <summary>
	/// Extended vertex data, includes tangents and secondary texture coordinates.<para/>
	/// NOTE: If raised, the second vertex buffer must contain extended vertex data matching the
	/// layout of type 'ExtendedVertex'.
	/// </summary>
	ExtendedSurfaceData = 2,
	/// <summary>
	/// Blend shape vertex data, includes indices and weights for each vertex.<para/>
	/// NOTE: If raised, the next vertex buffer after surface vertex data buffers must contain
	/// blend shape data matching the layout of type 'IndexedWeightedVertex',
	/// followed by a contiguous vertex buffer containing all vertex offsets for each of the
	/// indexed blend shapes.
	/// </summary>
	BlendShapes = 4,
	/// <summary>
	/// Bone animation vertex data, includes bone indices and weights for each vertex.<para/>
	/// NOTE: If raised, the next vertex buffer after blend shape data buffers must contain
	/// bone weight data matching the layout of type 'IndexedWeightedVertex',
	/// followed by a contiguous vertex buffer containing all bone matrices describing bone
	/// transformations in model space.
	/// </summary>
	Animations = 8,
	//...

	ALL = BasicSurfaceData | ExtendedSurfaceData | BlendShapes | Animations
}
