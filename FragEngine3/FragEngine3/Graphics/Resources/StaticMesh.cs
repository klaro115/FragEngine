using System.Numerics;
using FragEngine3.Graphics.Data;
using FragEngine3.Graphics.Internal;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public class StaticMesh : Mesh
	{
		#region Constructors

		/// <summary>
		/// Creates a new static 3D model, with polygonal surface geometry but without animations or blendshapes.<para/>
		/// NOTE: The model needs to initialized first by setting its geometry data at least once before first use.
		/// </summary>
		/// <param name="_handle">A resource handle for accessing and managing this model.</param>
		/// <param name="_core">The graphics core through which the model will be rendered.</param>
		/// <param name="_useFullSurfaceDef">Whether to use the full vertex definition, or only the basic surface
		/// geometry data. Basically, if your model needs tangent space or a second set of UV coordinates, set this
		/// to true.</param>
		public StaticMesh(ResourceHandle _handle, GraphicsCore _core, bool _useFullSurfaceDef) :
			base(_handle, _core, _useFullSurfaceDef)
		{
		}

		#endregion
		#region Fields

		private uint vertexCount = 0;
		private uint indexCount = 0;

		private BasicVertex[]? tempBasicData = null;
		private ExtendedVertex[]? tempExtData = null;

		protected ushort[]? tempIndexBuffer16 = null;
		protected int[]? tempIndexBuffer32 = null;

		#endregion
		#region Properties

		public override bool IsInitialized => !IsDisposed && vertexBufferBasic != null && (!useFullSurfaceDef || vertexBufferExt != null);
		public override bool IsUpToDate => IsInitialized && tempBasicData == null && tempExtData == null && tempIndexBuffer16 == null && tempIndexBuffer32 == null;

		public override uint VertexCount => vertexCount;
		public override uint IndexCount => indexCount;

		#endregion
		#region Methods

		public override VertexLayoutDescription[] GetVertexLayoutDesc()
		{
			return useFullSurfaceDef
				? GraphicsContants.SURFACE_VERTEX_LAYOUT_FULL
				: GraphicsContants.SURFACE_VERTEX_LAYOUT_BASIC;
		}

		public bool SetGeometry(in MeshSurfaceData _surfaceData)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set geometry on disposed static mesh!");
				return false;
			}
			if (_surfaceData == null || !_surfaceData.IsValid)
			{
				Console.WriteLine("Error! Cannot set geometry from null or invalid mesh surface data!");
				return false;
			}

			// Check if mesh definitions are complete; try to keep using current data if extended data is missing:
			if (useFullSurfaceDef && !_surfaceData.HasExtendedVertexData && _surfaceData.VertexCount > VertexCount)
			{
				Console.WriteLine("Error! Mesh surface data is missing extended vertex data, and cannot be substituted with current data!");
				return false;
			}

			// Set vertices:
			if (!SetBasicGeometry(_surfaceData.verticesBasic)) return false;

			if (useFullSurfaceDef && _surfaceData.HasExtendedVertexData)
			{
				if (!SetExtendedGeometry(_surfaceData.verticesExt!)) return false;
			}

			// Set indices:
			if (_surfaceData.IndexFormat == IndexFormat.UInt16)
			{
				return SetIndexData(_surfaceData.indices16!);
			}
			else
			{
				return SetIndexData(_surfaceData.indices32!);
			}
		}

		public override bool SetBasicGeometry(Vector3[] _positions, Vector3[] _normals, Vector2[] _uvs)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set basic geometry on disposed static mesh!");
				return false;
			}
			if (_positions == null || _normals == null || _uvs == null)
			{
				Console.WriteLine("Error! Cannot set basic geometry from null data arrays!");
				return false;
			}

			// Derive actual vertex count from data array with fewest entries:
			uint newVertexCount = (uint)Math.Min(Math.Min(_positions.Length, _normals.Length), _uvs.Length);

			// (Re)allocate vertex buffer to accomodate exact vertex count:
			if (vertexBufferBasic == null || vertexCount != newVertexCount)
			{
				vertexBufferBasic?.Dispose();

				uint byteSize = newVertexCount * BasicVertex.byteSize;
				BufferDescription bufferDesc = new(byteSize, BufferUsage.VertexBuffer);
				
				vertexBufferBasic = core.MainFactory.CreateBuffer(ref bufferDesc);
			}
			vertexCount = newVertexCount;

			// Store data in temporary buffer for later upload just before issuing draw calls:
			tempBasicData = new BasicVertex[vertexCount];
			for (int i = 0; i < vertexCount; ++i)
			{
				tempBasicData[i] = new BasicVertex()
				{
					position = _positions[i],
					normal = _normals[i],
					uv = _uvs[i],
				};
			}
			return true;
		}

		public override bool SetBasicGeometry(BasicVertex[] _verticesBasic)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set basic geometry on disposed static mesh!");
				return false;
			}
			if (_verticesBasic == null)
			{
				Console.WriteLine("Error! Cannot set basic geometry from null data array!");
				return false;
			}

			uint newVertexCount = (uint)_verticesBasic.Length;

			// (Re)allocate vertex buffer to accomodate exact vertex count:
			if (vertexBufferBasic == null || vertexCount != newVertexCount)
			{
				vertexBufferBasic?.Dispose();

				uint byteSize = newVertexCount * BasicVertex.byteSize;
				BufferDescription bufferDesc = new(byteSize, BufferUsage.VertexBuffer);

				vertexBufferBasic = core.MainFactory.CreateBuffer(ref bufferDesc);
			}
			vertexCount = newVertexCount;

			// Store data as temporary buffer for later upload just before issuing draw calls:
			tempBasicData = _verticesBasic;
			return true;
		}

		public override bool SetExtendedGeometry(Vector3[] _tangents, Vector2[] _uv2)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set extended geometry on disposed static mesh!");
				return false;
			}
			if (_tangents == null || _uv2 == null)
			{
				Console.WriteLine("Error! Cannot set extended geometry from null data arrays!");
				return false;
			}
			if (!useFullSurfaceDef)
			{
				Console.WriteLine($"Error! Static mesh '{resourceKey}' uses basic surface definition, extended geometry data is not supported!");
				return false;
			}

			Vector3[] newTangents = _tangents;
			Vector2[] newUv2 = _uv2;

			// Pad undefined or missing vertex data to match vertex count of basic data:
			uint extVertexCount = (uint)Math.Min(_tangents.Length, _uv2.Length);
			if (extVertexCount < vertexCount)
			{
				newTangents = new Vector3[vertexCount];
				newUv2 = new Vector2[vertexCount];
				Array.Copy(_tangents, newTangents, Math.Min(_tangents.Length, vertexCount));
				Array.Copy(_uv2, newUv2, Math.Min(_uv2.Length, vertexCount));
			}

			// Create secondary vertex buffer for extended surface data:
			if (vertexBufferExt == null)
			{
				vertexBufferExt?.Dispose();

				uint byteSize = vertexCount * ExtendedVertex.byteSize;
				BufferDescription bufferDesc = new(byteSize, BufferUsage.VertexBuffer);

				vertexBufferExt = core.MainFactory.CreateBuffer(ref bufferDesc);
			}

			// Store data in temporary buffer for later upload just before issuing draw calls:
			tempExtData = new ExtendedVertex[vertexCount];
			for (int i = 0; i < vertexCount; ++i)
			{
				tempExtData[i] = new ExtendedVertex()
				{
					tangent = newTangents[i],
					uv2 = newUv2[i],
				};
			}
			return true;
		}

		public override bool SetExtendedGeometry(ExtendedVertex[] _verticesExt)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set extended geometry on disposed static mesh!");
				return false;
			}
			if (_verticesExt == null)
			{
				Console.WriteLine("Error! Cannot set extended geometry from null data array!");
				return false;
			}
			if (!useFullSurfaceDef)
			{
				Console.WriteLine($"Error! Static mesh '{resourceKey}' uses basic surface definition, extended geometry data is not supported!");
				return false;
			}

			ExtendedVertex[] newVerticesExt = _verticesExt;

			// Pad undefined or missing vertex data to match vertex count of basic data:
			uint extVertexCount = (uint)_verticesExt.Length;
			if (extVertexCount < vertexCount)
			{
				newVerticesExt = new ExtendedVertex[vertexCount];
				Array.Copy(_verticesExt, newVerticesExt, Math.Min(_verticesExt.Length, vertexCount));
			}

			// Create secondary vertex buffer for extended surface data:
			if (vertexBufferExt == null)
			{
				vertexBufferExt?.Dispose();

				uint byteSize = vertexCount * ExtendedVertex.byteSize;
				BufferDescription bufferDesc = new(byteSize, BufferUsage.VertexBuffer);

				vertexBufferExt = core.MainFactory.CreateBuffer(ref bufferDesc);
			}

			// Store data as temporary buffer for later upload just before issuing draw calls:
			tempExtData = newVerticesExt;
			return true;
		}

		public override bool SetIndexData(ushort[] _indices, bool _verifyIndices = false)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set index data on disposed mesh!");
				return false;
			}
			if (_indices == null)
			{
				Console.WriteLine("Error! Cannot set index data from null index array!");
				return false;
			}

			IndexFormat = IndexFormat.UInt16;
			indexCount = (uint)_indices.Length;

			tempIndexBuffer16 = _indices;
			tempIndexBuffer32 = null;
			return true;
		}

		public override bool SetIndexData(int[] _indices, bool _verifyIndices = false)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot set index data on disposed mesh!");
				return false;
			}
			if (_indices == null)
			{
				Console.WriteLine("Error! Cannot set index data from null index array!");
				return false;
			}

			IndexFormat = _indices.Length < 65536 ? IndexFormat.UInt16 : IndexFormat.UInt32;
			indexCount = (uint)_indices.Length;

			if (IndexFormat == IndexFormat.UInt16)
			{
				tempIndexBuffer16 = new ushort[_indices.Length];
				for (int i = 0; i < _indices.Length; ++i)
				{
					tempIndexBuffer16[i] = (ushort)_indices[i];
				}
			}
			else
			{
				tempIndexBuffer16 = null;
				tempIndexBuffer32 = _indices;
			}
			return true;
		}

		public override bool GetGeometryBuffers(out DeviceBuffer[] _outVertexBuffers, out DeviceBuffer _outIndexBuffer)
		{
			// Gather static-only geometry buffers:
			if (!base.GetGeometryBuffers(out _outVertexBuffers, out _outIndexBuffer)) return false;

			// Update buffer contents just-in-time now that they are about to be used:
			if (tempBasicData != null)
			{
				core.Device.UpdateBuffer(_outVertexBuffers[0], 0, tempBasicData);
				tempBasicData = null;
			}
			if (useFullSurfaceDef && tempExtData != null)
			{
				core.Device.UpdateBuffer(_outVertexBuffers[1], 0, tempExtData);
				tempExtData = null;
			}
			if (tempIndexBuffer16 != null)
			{
				core.Device.UpdateBuffer(indexBuffer, 0, tempIndexBuffer16);
				tempIndexBuffer16 = null;
				tempIndexBuffer32 = null;
			}
			else if (tempIndexBuffer32 != null)
			{
				core.Device.UpdateBuffer(indexBuffer, 0, tempIndexBuffer32);
				tempIndexBuffer16 = null;
				tempIndexBuffer32 = null;
			}
			return true;
		}

		public override bool AsyncDownloadGeometry(AsyncGeometryDownloadRequest.CallbackReceiveDownloadedData _callbackDownloadDone)
		{
			if (!IsInitialized)
			{
				Console.WriteLine("Error! Cannot download geometry data from uninitialized mesh!");
				return false;
			}
			if (_callbackDownloadDone == null)
			{
				Console.WriteLine("Error! Cannot schedule async download of mesh geometry data with null callback function!");
				return false;
			}

			// If new data was just assigned this frame, and has yet to be uploaded to GPU, return this up-to-date data right away:
			if (tempBasicData != null &&
				(!useFullSurfaceDef || tempExtData != null) &&
				(tempIndexBuffer16 != null || tempIndexBuffer32 != null))
			{
				// Convert index data from 16-bit to 32-bit, if needed:
				int[] indexData;
				if (IndexFormat == IndexFormat.UInt16)
				{
					indexData = new int[tempIndexBuffer16!.Length];
					for (int i = 0; i < indexData.Length; ++i)
					{
						indexData[i] = tempIndexBuffer16![i];
					}
				}
				else
				{
					indexData = tempIndexBuffer32!;
				}

				// Send data off via callback right away:
				_callbackDownloadDone(this, tempBasicData, tempExtData, indexData);
				return true;
			}

			// If current data is not available CPU-side, request download:
			return base.AsyncDownloadGeometry(_callbackDownloadDone);
		}

		#endregion
	}
}

