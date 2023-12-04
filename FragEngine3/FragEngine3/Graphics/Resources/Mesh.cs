using System.Numerics;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Internal;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public abstract class Mesh : Resource
	{
		#region Constructors

		protected Mesh(ResourceHandle _handle, GraphicsCore _core, bool _useFullSurfaceDef) : base(_handle)
		{
			core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");
			useFullSurfaceDef = _useFullSurfaceDef;
		}

		protected Mesh(string _resourceKey, ResourceManager _resourceManager, GraphicsCore _core, bool _useFullSurfaceDef) : base(_resourceKey, _resourceManager)
		{
			core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");
			useFullSurfaceDef = _useFullSurfaceDef;
		}
		
		#endregion
		#region Fields

		public readonly GraphicsCore core;

		/// <summary>
		/// Whether this mesh uses the full extended vertex definition or just the basic surface data.<para/>
		/// BASIC: Only basic vertex data, enough for phong-shading a surface. Layout: [Pos, Norm, UV]<para/>
		/// FULL: Extended vertex data using a second vertex buffer, to allow more complex shading. Layout: [Tan, UV2]
		/// </summary>
		public readonly bool useFullSurfaceDef = false;

		protected DeviceBuffer? vertexBufferBasic = null;
		protected DeviceBuffer? vertexBufferExt = null;
		protected DeviceBuffer? indexBuffer = null;

		protected DeviceBuffer[] vertexBuffers = [];

		#endregion
		#region Properties

		public abstract bool IsInitialized { get; }
		/// <summary>
		/// Gets whether the GPU-side data of this mesh is up-to-date.<para/>
		/// Essentially, this will be true if the latest geometry data that was set has been successfully uploaded
		/// to GPU, or false, if the data upload is still pending. In general, upload should complete at the latest
		/// just-in-time before the mesh is used in draw calls.
		/// </summary>
		public abstract bool IsUpToDate { get; }

		public abstract uint VertexCount { get; }
		public abstract uint IndexCount { get; }
		public IndexFormat IndexFormat { get; protected set; } = IndexFormat.UInt16;

		public abstract float BoundingRadius { get; protected set; }

		public override ResourceType ResourceType => ResourceType.Model;

		protected Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsDisposed = true;

			vertexBufferBasic?.Dispose();
			vertexBufferExt?.Dispose();
			indexBuffer?.Dispose();

			if (_disposing)
			{
				vertexBuffers = [];
			}
		}

		public abstract VertexLayoutDescription[] GetVertexLayoutDesc();

		/// <summary>
		/// Gets the total number of vertex buffers required for drawing this mesh.
		/// </summary>
		/// <returns>The number of vertex buffers.</returns>
		public virtual int GetVertexBufferCount() => useFullSurfaceDef ? 2 : 1;

		/// <summary>
		/// Gets all vertex and index buffers required for drawing this mesh.
		/// </summary>
		/// <param name="_outVertexBuffers">Outputs an array of vertex buffers describing the mesh's surface and
		/// deformations. Depending on mesh type, the buffer order will be as follows, with optional buffers marked
		/// with an asterisk. The order will not change, even if optional buffers are skipped:<para/>
		/// <code>SurfaceBasic (0), SurfaceExt* (1), BlendShapes* (2), BoneAnim* (3)</code><para/>
		/// The layout of vertex data layouts for each of these buffers will look roughly as follows:<para/>
		/// 0. SurfaceBasic: [Pos, Norm, Tex]<para/>
		/// 1. SurfaceExt: [Tan, Tex2]<para/>
		/// 2. BlendShapes: [BlendIdx, BlendWeight]<para/>
		/// 3. BoneAnim: [BoneIdx, BoneWeight]</param>
		/// <param name="_outIndexBuffer">Outputs the index buffer describing which vertices form triangular polygon surfaces.</param>
		/// <param name="_outVertexDataFlags">Outputs flags for the vertex data exposed by this mesh's vertex buffers.</param>
		/// <returns>True if buffers could be retrieved, false otherwise.</returns>
		public virtual bool GetGeometryBuffers(out DeviceBuffer[] _outVertexBuffers, out DeviceBuffer _outIndexBuffer, out MeshVertexDataFlags _outVertexDataFlags)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot get geometry buffers of disposed mesh!");
				_outVertexBuffers = [];
				_outIndexBuffer = null!;
				_outVertexDataFlags = 0;
				return false;
			}
			if (!IsInitialized || vertexBufferBasic == null || indexBuffer == null)
			{
				Logger.LogError($"Cannot get geometry buffers; mesh '{resourceKey}' has not been initialized!");
				_outVertexBuffers = [];
				_outIndexBuffer = null!;
				_outVertexDataFlags = 0;
				return false;
			}

			int vertexBufferCount = GetVertexBufferCount();
			if (vertexBuffers == null || vertexBuffers.Length != vertexBufferCount)
			{
				vertexBuffers = new DeviceBuffer[vertexBufferCount];
				vertexBuffers[0] = vertexBufferBasic;
				if (vertexBufferCount > 1)
				{
					vertexBuffers[1] = vertexBufferExt!;
				}
			}

			_outVertexBuffers = vertexBuffers;
			_outIndexBuffer = indexBuffer;
			_outVertexDataFlags = useFullSurfaceDef
				? MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData
				: MeshVertexDataFlags.BasicSurfaceData;
			return true;
		}

		public abstract bool SetBasicGeometry(
			Vector3[] _positions,
			Vector3[] _normals,
			Vector2[] _uvs);
		public abstract bool SetBasicGeometry(
			BasicVertex[] _verticesBasic);

		public abstract bool SetExtendedGeometry(
			Vector3[] _tangents,
			Vector2[] _uv2);
		public abstract bool SetExtendedGeometry(
			ExtendedVertex[] _verticesExt);

		public virtual bool SetFullGeometry(
			Vector3[] _positions,
			Vector3[] _normals,
			Vector3[] _tangents,
			Vector2[] _uvs,
			Vector2[] _uv2)
		{
			return SetBasicGeometry(_positions, _normals, _uvs) && SetExtendedGeometry(_tangents, _uvs);
		}
		public virtual bool SetFullGeometry(
			BasicVertex[] _verticesBasic,
			ExtendedVertex[] _verticesExt)
		{
			return SetBasicGeometry(_verticesBasic) && SetExtendedGeometry(_verticesExt);
		}


		public abstract bool SetIndexData(ushort[] _indices, bool _verifyIndices = false);
		public abstract bool SetIndexData(int[] _indices, bool _verifyIndices = false);

		public virtual bool AsyncDownloadGeometry(AsyncGeometryDownloadRequest.CallbackReceiveDownloadedData _callbackDownloadDone)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot download geometry data from uninitialized mesh!");
				return false;
			}
			if (_callbackDownloadDone == null)
			{
				Logger.LogError("Cannot schedule download of mesh geometry data with null callback function!");
				return false;
			}

			AsyncGeometryDownloadRequest request = new(this, CallbackDispatchCopy, CallbackDownloadData, _callbackDownloadDone)
			{
				dstBasicDataBuffer = new BasicVertex[VertexCount],
				dstExtendedDataBuffer = useFullSurfaceDef ? new ExtendedVertex[VertexCount] : null,
				dstIndexBuffer = new int[IndexCount],
			};
			
			return core.ScheduleAsyncGeometryDownload(request);


			bool CallbackDispatchCopy(CommandList _cmdList, AsyncGeometryDownloadRequest _request, out DeviceBuffer[] _outStagingBuffers)
			{
				// Calculate and prepare buffer sizes:
				int geometryBufferCount = GetVertexBufferCount() + 1;
				int lastIdx = geometryBufferCount - 1;
				_outStagingBuffers = new DeviceBuffer[geometryBufferCount];

				uint sizeBasic = BasicVertex.byteSize * _request.vertexCount;
				uint sizeExt = ExtendedVertex.byteSize * _request.vertexCount;
				uint sizeIndex = (uint)(IndexFormat == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(int)) * _request.indexCount;

				// Allocate temporary staging buffers from which we can download to CPU memory:
				BufferDescription descBasic = new(sizeBasic, BufferUsage.Staging);
				BufferDescription descExt = new(sizeExt, BufferUsage.Staging);
				BufferDescription descIndex = new(sizeIndex, BufferUsage.Staging);

				_outStagingBuffers[0] = core.MainFactory.CreateBuffer(ref descBasic);
				if (useFullSurfaceDef && vertexBufferExt != null)
				{
					_outStagingBuffers[1] = core.MainFactory.CreateBuffer(ref descExt);
				}
				_outStagingBuffers[lastIdx] = core.MainFactory.CreateBuffer(ref descIndex);

				// Issue copy commands:
				_cmdList.CopyBuffer(vertexBufferBasic, 0, _outStagingBuffers[0], 0, sizeBasic);
				if (useFullSurfaceDef && vertexBufferExt != null)
				{
					_cmdList.CopyBuffer(vertexBufferExt, 0, _outStagingBuffers[1], 0, sizeExt);
				}
				_cmdList.CopyBuffer(indexBuffer, 0, _outStagingBuffers[lastIdx], 0, sizeIndex);

				return true;
			}

			bool CallbackDownloadData(AsyncGeometryDownloadRequest _request)
			{
				int lastIdx = _request.stagingBuffers!.Length - 1;

				// Prepare CPU-side output buffers, if those haven't been pre-allocated:
				_request.dstBasicDataBuffer ??= new BasicVertex[_request.vertexCount];
				_request.dstExtendedDataBuffer ??= useFullSurfaceDef ? new ExtendedVertex[_request.vertexCount] : null;
				_request.dstIndexBuffer ??= new int[_request.indexCount];

				DeviceBuffer sbBasic = _request.stagingBuffers![0];
				DeviceBuffer? sbExt = useFullSurfaceDef ? _request.stagingBuffers![1] : null;
				DeviceBuffer sbIndex = _request.stagingBuffers![lastIdx];

				// Download basic vertex data:
				MappedResourceView<BasicVertex> viewBasic = core.Device.Map<BasicVertex>(sbBasic, MapMode.Read);
				for (int i = 0; i < _request.vertexCount; ++i)
				{
					_request.dstBasicDataBuffer[i] = viewBasic[i];
				}
				core.Device.Unmap(sbBasic);

				// Download extended vertex data:
				if (useFullSurfaceDef)
				{
					MappedResourceView<ExtendedVertex> viewExt = core.Device.Map<ExtendedVertex>(sbExt, MapMode.Read);
					for (int i = 0; i < _request.vertexCount; ++i)
					{
						_request.dstExtendedDataBuffer![i] = viewExt[i];
					}
					core.Device.Unmap(sbExt);
				}

				// Download index data:
				if (IndexFormat == IndexFormat.UInt16)
				{
					// 16-bit indices:
					MappedResourceView<ushort> viewIndex = core.Device.Map<ushort>(sbIndex, MapMode.Read);
					for (int i = 0; i < _request.indexCount; ++i)
					{
						_request.dstIndexBuffer[i] = viewIndex[i];
					}
					core.Device.Unmap(sbIndex);
				}
				else
				{
					// 32-bit indices:
					MappedResourceView<int> viewIndex = core.Device.Map<int>(sbIndex, MapMode.Read);
					for (int i = 0; i < _request.indexCount; ++i)
					{
						_request.dstIndexBuffer[i] = viewIndex[i];
					}
					core.Device.Unmap(sbIndex);
				}

				return true;
			}
		}
		
		public override IEnumerator<ResourceHandle> GetResourceDependencies()
		{
			if (GetResourceHandle(out ResourceHandle handle))
			{
				yield return handle;
			}
		}

		#endregion
	}
}

