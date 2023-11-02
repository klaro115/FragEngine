using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	/// <summary>
	/// Indices for weighted vertex data, such as for blend shapes or bone animations. These indices
	/// are used to retrieve a set of 4 offsets or bone transformations that should be applied to a
	/// vertex.<para/>
	/// NOTE: The index 0 is reserved for "unassigned" blend shapes or bones, meaning the first entry
	/// in the corresponding data arrays must be zero or an identity transformation.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = (int)byteSize)]
	public struct VertexWeightIndices
	{
		#region Constructors

		public VertexWeightIndices(uint _index0, uint _index1, uint _index2, uint _index3)
		{
			index0 = _index0;
			index1 = _index1;
			index2 = _index2;
			index3 = _index3;
		}
		public VertexWeightIndices(Vector4 _indices)
		{
			index0 = (uint)_indices.X;
			index1 = (uint)_indices.Y;
			index2 = (uint)_indices.Z;
			index3 = (uint)_indices.W;
		}

		#endregion
		#region Fields

		public uint index0;
		public uint index1;
		public uint index2;
		public uint index3;

		#endregion
		#region Constants

		public const uint byteSize = 4 * sizeof(uint);

		#endregion
		#region Methods

		public override readonly string ToString()
		{
			return $"({index0}; {index1}; {index2}; {index3})";
		}

		#endregion
	}

	/// <summary>
	/// Vertex data definition for basic surface geometry. This is what one element in the primary vertex buffer looks like.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = (int)byteSize)]
	public struct BasicVertex
	{
		#region Fields

		public Vector3 position;
		public Vector3 normal;
		public Vector2 uv;

		#endregion
		#region Constants

		public const uint byteSize = 8 * sizeof(float);

		public static readonly VertexLayoutDescription vertexLayoutDesc = new(
			byteSize,
			new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3, 0),
			new VertexElementDescription("Normal", VertexElementSemantic.Normal, VertexElementFormat.Float3, 3 * sizeof(float)),
			new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 6 * sizeof(float)));

		#endregion
		#region Methods

		public override readonly string ToString()
		{
			return $"(Pos: {position}, Norm: {normal}, Tex: {uv})";
		}

		#endregion
	}

	/// <summary>
	/// Vertex data definition for extended surface geometry. This is what one element in a secondary vertex buffer may look like.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = (int)byteSize)]
	public struct ExtendedVertex
	{
		#region Fields

		public Vector3 tangent;
		public Vector2 uv2;

		#endregion
		#region Constants

		public const uint byteSize = 5 * sizeof(float);

		public static readonly VertexLayoutDescription vertexLayoutDesc = new(
			byteSize,
			new VertexElementDescription("Tangent", VertexElementSemantic.Normal, VertexElementFormat.Float3, 0),
			new VertexElementDescription("UV2", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 3 * sizeof(float)));

		#endregion
		#region Methods

		public override readonly string ToString()
		{
			return $"(Tan: {tangent}, Tex2: {uv2})";
		}

		#endregion
	}

	/// <summary>
	/// Vertex data definition for indexed and weighted surface data, such as for blend shapes or bone animations.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = (int)byteSize)]
	public struct IndexedWeightedVertex
	{
		#region Fields

		public VertexWeightIndices indices;
		public Vector4 weights;

		#endregion
		#region Constants

		public const uint byteSize = VertexWeightIndices.byteSize + 4 * sizeof(float);

		public static readonly VertexLayoutDescription vertexLayoutDesc = new(
			byteSize,
			new VertexElementDescription("Indices", VertexElementSemantic.Normal, VertexElementFormat.UInt4, 0),
			new VertexElementDescription("Weights", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4, 4 * sizeof(uint)));

		#endregion
		#region Methods

		public override readonly string ToString()
		{
			return $"(Indices: {indices}, Weights: {weights})";
		}

		#endregion
	}

	/// <summary>
	/// Flags describing which vertex data is present or must be present for a mesh.
	/// </summary>
	[Flags]
	public enum MeshVertexDataFlags
	{
		/// <summary>
		/// Basic vertex data, includes position, normals, and texture coordinates.<para/>
		/// NOTE: This must always be the first vertex buffer that is bound for standard surface meshes.
		/// Data must match the type '<see cref="BasicVertex"/>'.
		/// </summary>
		BasicSurfaceData	= 1,
		/// <summary>
		/// Extended vertex data, includes tangents and secondary texture coordinates.<para/>
		/// NOTE: If raised, the second vertex buffer must contain extended vertex data matching the
		/// layout of type '<see cref="ExtendedVertex"/>'.
		/// </summary>
		ExtendedSurfaceData	= 2,
		/// <summary>
		/// Blend shape vertex data, includes indices and weights for each vertex.<para/>
		/// NOTE: If raised, the next vertex buffer after surface vertex data buffers must contain
		/// blend shape data matching the layout of type '<see cref="IndexedWeightedVertex"/>',
		/// followed by a contiguous vertex buffer containing all vertex offsets for each of the
		/// indexed blend shapes.
		/// </summary>
		BlendShapes			= 4,
		/// <summary>
		/// Bone animation vertex data, includes bone indices and weights for each vertex.<para/>
		/// NOTE: If raised, the next vertex buffer after blend shape data buffers must contain
		/// bone weight data matching the layout of type '<see cref="IndexedWeightedVertex"/>',
		/// followed by a contiguous vertex buffer containing all bone matrices describing bone
		/// transformations in model space.
		/// </summary>
		Animations			= 8,
		//...

		ALL					= BasicSurfaceData | ExtendedSurfaceData | BlendShapes | Animations
	}
}
