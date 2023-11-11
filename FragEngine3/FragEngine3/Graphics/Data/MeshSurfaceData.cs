using System.Numerics;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Data
{
	/// <summary>
	/// Raw CPU-side surface geometry data for a mesh. This contains basic and (optionally) extended vertex data defining
	/// a hard surface mesh. A '<see cref="StaticMesh"/>' resource can be created directly from just this data; animated
	/// meshes will require additional data.
	/// </summary>
	public sealed class MeshSurfaceData
	{
		#region Fields

		public BasicVertex[] verticesBasic = Array.Empty<BasicVertex>();
		public ExtendedVertex[]? verticesExt = null;

		public ushort[]? indices16 = null;
		public int[]? indices32 = null;

		#endregion
		#region Properties

		/// <summary>
		/// Gets whether the data is assigned and index count is valid. False if no data is set, or if triangle index count
		/// count is no a multiple of 3.
		/// </summary>
		public bool IsValid => VertexCount != 0 && IndexCount >= 3;

		/// <summary>
		/// Gets the number of vertices in this mesh, based in basic vertex data only.
		/// </summary>
		public int VertexCount => verticesBasic != null ? verticesBasic.Length : 0;
		/// <summary>
		/// Gets the number of triangle indices in this mesh. This should be a multiple of 3. If both 16-bit and 32-bit index
		/// buffers are assigned, the 16-bit indices will be preferred and its length returned.
		/// </summary>
		public int IndexCount => indices16 != null ? indices16.Length : (indices32 != null ? indices32.Length : 0);
		/// <summary>
		/// Gets the number of triangles defined by the index buffer of this mesh. Trailing incomplete triangles with fewer
		/// than 3 indices will be ignored.
		/// </summary>
		public int TriangleCount => IndexCount / 3;

		/// <summary>
		/// Gets whether this mesh has extended vertex data. True if extended data buffer is non-null and contains at least
		/// the same number of elements as the basic vertex buffer.
		/// </summary>
		public bool HasExtendedVertexData => verticesExt != null && verticesExt.Length >= VertexCount;
		/// <summary>
		/// Gets the index format to be used for this mesh, either 16-bit or 32-bit.<para/>
		/// NOTE: If both 16-bit and 32-bit buffers are assigned, 16-bit will be preferred. Unassign the 16-bit buffer to
		/// force usage of an assigned 32-bit index buffer. Meshes with more than 65K vertices must use the 32-bit index
		/// format.
		/// </summary>
		public IndexFormat IndexFormat => indices16 != null ? IndexFormat.UInt16 : IndexFormat.UInt32;

		#endregion
		#region Methods

		/// <summary>
		/// Creates a deep copy of this mesh.
		/// </summary>
		/// <returns>An exact copy of the mesh.</returns>
		public MeshSurfaceData Clone()
		{
			return new()
			{
				verticesBasic = verticesBasic != null ? (BasicVertex[])verticesBasic.Clone() : Array.Empty<BasicVertex>(),
				verticesExt = verticesExt,

				indices16 = (ushort[]?)indices16?.Clone(),
				indices32 = (int[]?)indices32?.Clone(),
			};
		}

		public bool SetBasicVertexData(IList<Vector3> _positions, IList<Vector3> _normals, IList<Vector2> _uvs, int _overrideVertexCount = -1)
		{
			if (_positions == null || _normals == null || _uvs == null)
			{
				Logger.Instance?.LogError("Cannot set basic vertex data from null data arrays!");
				return false;
			}

			int newVertexCount = _overrideVertexCount < 0
				? Math.Min(Math.Min(_positions.Count, _normals.Count), _uvs.Count)
				: _overrideVertexCount;

			if (verticesBasic == null || verticesBasic.Length != newVertexCount)
			{
				verticesBasic = new BasicVertex[newVertexCount];
			}

			for (int i = 0; i < newVertexCount; ++i)
			{
				verticesBasic[i] = new()
				{
					position = _positions[i],
					normal = _normals[i],
					uv = _uvs[i],
				};
			}
			return true;
		}

		public bool SetExtendedVertexData(IList<Vector3> _tangents, IList<Vector2> _uvs2, int _overrideVertexCount = -1)
		{
			if (_tangents == null || _uvs2 == null)
			{
				Logger.Instance?.LogError("Cannot set extended vertex data from null data arrays!");
				return false;
			}

			int newVertexCount = _overrideVertexCount < 0
				? Math.Min(_tangents.Count, _uvs2.Count)
				: _overrideVertexCount;

			if (verticesExt == null || verticesExt.Length != newVertexCount)
			{
				verticesExt = new ExtendedVertex[newVertexCount];
			}

			for (int i = 0; i < newVertexCount; ++i)
			{
				verticesExt[i] = new()
				{
					tangent = _tangents[i],
					uv2 = _uvs2[i],
				};
			}
			return true;
		}

		public bool SetIndexData(IList<int> _indices, int _overrideIndexCount = -1)
		{
			if (_indices == null)
			{
				Logger.Instance?.LogError("Cannot set index data from null source array! (32-bit indices)");
				return false;
			}
			int newIndexCount = _overrideIndexCount < 0 ? _indices.Count : Math.Min(_overrideIndexCount, _indices.Count);
			if (newIndexCount % 3 != 0)
			{
				Logger.Instance?.LogError("Mesh index count must be a multiple of 3! (32-bit indices)");
				return false;
			}

			if (indices32 == null || indices32.Length != newIndexCount)
			{
				indices32 = new int[newIndexCount];
			}

			if (_indices.Count == newIndexCount)
			{
				_indices.CopyTo(indices32, 0);
			}
			else
			{
				for (int i = 0; i < newIndexCount; ++i)
				{
					indices32[i] = _indices[i];
				}
			}
			return true;
		}

		public bool SetIndexData(IList<ushort> _indices, int _overrideIndexCount = -1)
		{
			if (_indices == null)
			{
				Logger.Instance?.LogError("Cannot set index data from null source array! (16-bit indices)");
				return false;
			}
			int newIndexCount = _overrideIndexCount < 0 ? _indices.Count : Math.Min(_overrideIndexCount, _indices.Count);
			if (newIndexCount % 3 != 0)
			{
				Logger.Instance?.LogError("Mesh index count must be a multiple of 3! (16-bit indices)");
				return false;
			}

			// Enforce 32-bit indices if more than 65K vertices:
			if (VertexCount > ushort.MaxValue)
			{
				if (indices32 == null || indices32.Length != newIndexCount)
				{
					indices32 = new int[newIndexCount];
				}
				indices16 = null;

				for (int i = 0; i < newIndexCount; ++i)
				{
					indices32[i] = _indices[i];
				}
			}
			// Keep using 16-bit indices if fewer than 65K vertices:
			else
			{
				if (indices16 == null || indices16.Length != newIndexCount)
				{
					indices16 = new ushort[newIndexCount];
				}
				indices32 = null;

				if (_indices.Count == newIndexCount)
				{
					_indices.CopyTo(indices16, 0);
				}
				else
				{
					for (int i = 0; i < newIndexCount; ++i)
					{
						indices16[i] = _indices[i];
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Calculates the bounding box minima and maxima coordinates of vertices within this mesh.
		/// </summary>
		/// <param name="_outMin">Outputs the minima for vertex coordinates along all 3 axes.</param>
		/// <param name="_outMax">Outputs the maxima for vertex coordinates along all 3 axes.</param>
		/// <returns>True if the mesh contains vertices, false if no vertex data is set.</returns>
		public bool CalculateBoundingBox(out Vector3 _outMin, out Vector3 _outMax)
		{
			if (VertexCount == 0)
			{
				_outMin = Vector3.Zero;
				_outMax = Vector3.Zero;
				return false;
			}

			_outMin = new Vector3(1.0e+8f, 1.0e+8f, 1.0e+8f);
			_outMax = new Vector3(-1.0e+8f, -1.0e+8f, -1.0e+8f);

			for (int i = 0; i < verticesBasic.Length; ++i)
			{
				Vector3 pos = verticesBasic[i].position;
				_outMin = Vector3.Min(_outMin, pos);
				_outMax = Vector3.Max(_outMax, pos);
			}
			return true;
		}

		/// <summary>
		/// Apply a transformation to all vertices of this mesh.
		/// </summary>
		/// <param name="_pose">A pose describing the transformation.</param>
		public void TransformVertices(Pose _pose)
		{
			if (VertexCount == 0) return;

			if (HasExtendedVertexData)
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					BasicVertex vb = verticesBasic[i];
					ExtendedVertex ve = verticesExt![i];

					vb.position = _pose.TransformPoint(vb.position);
					vb.normal = _pose.TransformDirection(vb.normal);
					ve.tangent = _pose.TransformDirection(ve.tangent);

					verticesBasic[i] = vb;
					verticesExt![i] = ve;
				}
			}
			else
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					BasicVertex vb = verticesBasic[i];

					vb.position = _pose.TransformPoint(vb.position);
					vb.normal = _pose.TransformDirection(vb.normal);

					verticesBasic[i] = vb;
				}
			}
		}

		/// <summary>
		/// Apply a transformation to all vertices of this mesh.
		/// </summary>
		/// <param name="_mtxTransformation">A matrix describing the transformation.</param>
		public void TransformVertices(Matrix4x4 _mtxTransformation)
		{
			if (VertexCount == 0) return;

			if (HasExtendedVertexData)
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					BasicVertex vb = verticesBasic[i];
					ExtendedVertex ve = verticesExt![i];


					vb.position = Vector3.Transform(vb.position, _mtxTransformation);
					vb.normal = Vector3.TransformNormal(vb.normal, _mtxTransformation);
					ve.tangent = Vector3.TransformNormal(ve.tangent, _mtxTransformation);

					verticesBasic[i] = vb;
					verticesExt![i] = ve;
				}
			}
			else
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					BasicVertex vb = verticesBasic[i];

					vb.position = Vector3.Transform(vb.position, _mtxTransformation);
					vb.normal = Vector3.TransformNormal(vb.normal, _mtxTransformation);

					verticesBasic[i] = vb;
				}
			}
		}

		/// <summary>
		/// Normalize lengths of all direction vectors (normals, tangents, etc.) of this mesh's vertex data to 1.
		/// </summary>
		public void NormalizeVectors()
		{
			if (verticesBasic != null)
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					verticesBasic[i].normal = Vector3.Normalize(verticesBasic[i].normal);
				}
			}
			if (verticesExt != null)
			{
				for (int i = 0; i < verticesExt.Length; ++i)
				{
					verticesExt[i].tangent = Vector3.Normalize(verticesExt[i].tangent);
				}
			}
		}

		/// <summary>
		/// Reverse vertex order of faces, thus flipping them inside-out. Optionally, also reverse direction of normal
		/// and tangent vectors on all vertices.
		/// </summary>
		/// <param name="_flipNormals">Whether to reverse orientation of all normal vectors.</param>
		/// <param name="_flipTangents">Whether to reverse orientation of all tangent vectors. Only relevant for meshes
		/// that have extended vertex data.</param>
		public void ReverseVertexOrder(bool _flipNormals, bool _flipTangents)
		{
			int indexCount = IndexCount;

			// Flip order of the 2nd and 3rd index of each triangle:
			if (indices16 != null && indices16.Length >= 3)
			{
				for (int i = 0; i < indexCount; i += 3)
				{
					ushort idx1 = indices16[i + 1];
					ushort idx2 = indices16[i + 2];
					indices16[i + 1] = idx2;
					indices16[i + 2] = idx1;
				}
			}
			else if (indices32 != null && indices32.Length >= 3)
			{
				for (int i = 0; i < indexCount; i += 3)
				{
					int idx1 = indices32[i + 1];
					int idx2 = indices32[i + 2];
					indices32[i + 1] = idx2;
					indices32[i + 2] = idx1;
				}
			}

			// If requested, also invert direction of all normal and tangent vectors:
			if (_flipNormals && _flipTangents && HasExtendedVertexData)
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					verticesBasic[i].normal *= -1;
					verticesExt![i].tangent *= -1;
				}
			}
			else if (_flipNormals)
			{
				for (int i = 0; i < verticesBasic.Length; ++i)
				{
					verticesBasic[i].normal *= -1;
				}
			}
			else if (_flipTangents && HasExtendedVertexData)
			{
				for (int i = 0; i < verticesExt!.Length; ++i)
				{
					verticesExt![i].tangent *= -1;
				}
			}
		}

		public override string ToString()
		{
			return $"Mesh Surface Data (Vertices: {VertexCount}, Indices: {IndexCount}, Triangles: {TriangleCount}, Format: {IndexFormat})";
		}

		#endregion
	}
}
