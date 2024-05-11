using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using Veldrid;

namespace FragEngine3.Graphics.Internal
{
	/// <summary>
	/// Request for asynchronous download of surface geometry data of a mesh. This will copy data of vertex buffers
	/// and index buffers each to a staging buffer, before then downloading it all to CPU-side memory after the frame's
	/// draw calls have been issued.
	/// </summary>
	public sealed class AsyncGeometryDownloadRequest : IDisposable
	{
		#region Types

		/// <summary>
		/// Callback function for issuing copy/blitting commands through a graphic device's command list.
		/// </summary>
		/// <param name="_blittingCmdList">The command list through which the copy is enacted.</param>
		/// <param name="_request">The request through which the geometry download was initiated. During buffer
		/// allocation, the vertex and index count recorded by the request should be used, rather than the then
		/// current values from the mesh. It is, after all, possible for the geometry data to have been changed
		/// after the download request was issued.</param>
		/// <param name="_outStagingBuffer">Outputs an array of staging buffers to which data will be copied for
		/// later download to CPU memory. Staging buffers offer CPU-side memory read access, unlike dynamic or
		/// other buffer types. On failure, or an empty array should be output instead.</param>
		/// <param name="_outStagingBufferDataTypes">Outputs an array of mesh vertex data flags, one for each
		/// staging buffer. The raised bit flag indicates what kind of geometry data is contained in its staging
		/// buffer. The index buffer will be the last element, with no vertex flags raised.</param>
		/// <returns>True if copy command could be issued, and staging buffers created, false otherwise.</returns>
		public delegate bool CallbackDispatchCopy(CommandList _blittingCmdList, AsyncGeometryDownloadRequest _request, out DeviceBuffer[] _outStagingBuffer, out MeshVertexDataFlags[] _outStagingBufferDataTypes);

		/// <summary>
		/// Callback function for retrieving copied buffer data from the previously allocated and copied-to
		/// staging buffers.
		/// </summary>
		/// <param name="_request">The request through which the geometry download was initiated, and which
		/// holds all staging buffers and output arrays. Output arrays may have been pre-allocated during request
		/// creation; if they weren't, those arrays should be allocated during this call. The sizes of destination
		/// arrays should be derived from the request's <see cref="vertexCount"/> and <see cref="indexCount"/>.</param>
		/// <returns>True if the download from GPU buffer succeeded, and data was written to destination buffers, false otherwise.</returns>
		public delegate bool CallbackDownloadData(AsyncGeometryDownloadRequest _request);

		/// <summary>
		/// Callback function for when geometry data was downloaded from GPU. This will be called at the end of the
		/// current frame's draw calls. Data may be used during the next frame's update stages.
		/// </summary>
		/// <param name="_mesh">The mesh from which the geometry data was downloaded.</param>
		/// <param name="_meshSurfaceData">The surface geometry data, read from the basic and extended vertex buffers, as well as the index buffer.</param>
		/// <param name="_blendShapeData">The blend shape vertex data. This will be null if the mesh does not have any blend shapes.</param>
		/// <param name="_animationData">The bone animation vertex data. This will be null if the mesh does not have an armature or bone weights.</param>
		public delegate void CallbackReceiveDownloadedData(Mesh _mesh, MeshSurfaceData _meshSurfaceData, IndexedWeightedVertex[]? _blendShapeData = null, IndexedWeightedVertex[]? _animationData = null);

		#endregion
		#region Constructors

		public AsyncGeometryDownloadRequest(
			Mesh _mesh,
			CallbackDispatchCopy _callbackDispatchCopy,
			CallbackDownloadData _callbackDownloadData,
			CallbackReceiveDownloadedData _callbackReceiveDownloadedData)
		{
			mesh = _mesh ?? throw new ArgumentNullException(nameof(_mesh), "Mesh may not be null!");
			vertexCount = mesh.VertexCount;
			indexCount = mesh.IndexCount;

			callbackDispatchCopy = _callbackDispatchCopy;
			callbackDownloadData = _callbackDownloadData;
			callbackReceiveDownloadedData = _callbackReceiveDownloadedData;
		}

		~AsyncGeometryDownloadRequest()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Mesh mesh;
		public readonly uint vertexCount;
		public readonly uint indexCount;

		private readonly CallbackDispatchCopy callbackDispatchCopy;
		private readonly CallbackDownloadData callbackDownloadData;
		public readonly CallbackReceiveDownloadedData callbackReceiveDownloadedData;

		public MeshSurfaceData dstSurfaceData = new();
		public IndexedWeightedVertex[]? dstBlendShapeData = null;
		public IndexedWeightedVertex[]? dstAnimationData = null;

		#endregion
		#region Properties

		public bool IsValid => !IsDisposed && mesh.IsInitialized && callbackDispatchCopy is not null && callbackReceiveDownloadedData is not null && dstSurfaceData is not null;
		public bool IsDisposed { get; private set; } = false;

		public DeviceBuffer[]? StagingBuffers { get; private set; } = null;
		public MeshVertexDataFlags[]? StagingBufferDataTypes { get; private set; } = null;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _)
		{
			IsDisposed = true;

			if (StagingBuffers is not null)
			{
				foreach (DeviceBuffer buffer in StagingBuffers)
				{
					buffer?.Dispose();
				}
			}
		}
		
		internal bool Dispatch(CommandList _blittingCmdList)
		{
			if (!IsValid) return false;
			if (_blittingCmdList is null || _blittingCmdList.IsDisposed) return false;

			if (!callbackDispatchCopy(_blittingCmdList, this, out DeviceBuffer[] stagingBuffers, out MeshVertexDataFlags[] stagingBufferDataTypes))
			{
				return false;
			}

			StagingBuffers = stagingBuffers;
			StagingBufferDataTypes = stagingBufferDataTypes;
			return true;
		}

		internal bool Finish()
		{
			if (!IsValid) return false;
			if (StagingBuffers is null) return false;

			// Call for data to be downloaded from staging buffers:
			if (!callbackDownloadData(this))
			{
				return false;
			}

			// If data failed to be downloaded or no destination arrays were assigned, abort:
			if (dstSurfaceData.verticesBasic is null || (dstSurfaceData.indices16 is null && dstSurfaceData.indices32 is null))
			{
				return false;
			}

			// Send out downloaded data to whomever was the intended recipient:
			callbackReceiveDownloadedData(mesh, dstSurfaceData, dstBlendShapeData, dstAnimationData);
			return true;
		}

		#endregion
	}
}
