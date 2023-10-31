using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Internal
{
	/// <summary>
	/// Request for asynchronous download of surface geometry data of a mesh. This will copy data of vertex buffers
	/// (basic and extended) and index buffers each to a staging buffer, before then downloading it all to CPU-side
	/// memory after the frame's draw calls have been issued.
	/// </summary>
	public sealed class AsyncGeometryDownloadRequest : IDisposable
	{
		#region Types

		/// <summary>
		/// Callback function for issuing copy/blitting commands through a graphic device's command list.
		/// </summary>
		/// <param name="_cmdList">The command list through which the copy is enacted.</param>
		/// <param name="_request">The request through which the geometry download was initiated. During buffer
		/// allocation, the vertex and index count recorded by the request should be used, rather than the then
		/// current values from the mesh. It is, after all, possible for the geometry data to have been changed
		/// after the download request was issued.</param>
		/// <param name="_outStagingBuffer">Outputs an array of staging buffers to which data will be copied for
		/// later download to CPU memory. Staging buffers offer CPU-side memory read access, unlike dynamic or
		/// other buffer types. On failure, or an empty array should be output instead.</param>
		/// <returns>True if copy command could be issues, and staging buffers created, false otherwise.</returns>
		public delegate bool CallbackDispatchCopy(CommandList _cmdList, AsyncGeometryDownloadRequest _request, out DeviceBuffer[] _outStagingBuffer);

		/// <summary>
		/// Callback function for retrieving copied buffer data from the previously allocated and copied-to
		/// staging buffers.
		/// </summary>
		/// <param name="_request">The request through which the geometry download was initiated, and which
		/// holds all staging buffers and output arrays. Output arrays may have been pre-allocated during request
		/// creation; if they weren't, those arrays should be allocated during this call. The sizes of destination
		/// arrays should be derived from the request's <see cref="vertexCount"/> and <see cref="indexCount"/>.</param>
		/// <returns></returns>
		public delegate bool CallbackDownloadData(AsyncGeometryDownloadRequest _request);

		/// <summary>
		/// Callback function for when geometry data was downloaded from GPU. This will be called at the end of the
		/// current frame's draw calls. Data may be used during the next frame's update stages.
		/// </summary>
		/// <param name="_mesh">The mesh from which the geometry data was downloaded.</param>
		/// <param name="_basicData">The basic vertex data, read from the primary vertex buffer.</param>
		/// <param name="_extendedData">Extended vertex data, read from an optional secondary vertex buffer.<para/>
		/// This will be null if the mesh's '<see cref="Mesh.useFullSurfaceDef"/>' is set to false, as only basic
		/// surface geometry data will have been assigned and used.</param>
		/// <param name="_indices">Triangle indices, with each set of three indices referencing the three vertices
		/// that make up one triangle face. Length will be a multiple of 3.</param>
		public delegate void CallbackReceiveDownloadedData(Mesh _mesh, Mesh.BasicVertex[] _basicData, Mesh.ExtendedVertex[]? _extendedData, int[] _indices);

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

		public DeviceBuffer[]? stagingBuffers = null;

		public Mesh.BasicVertex[]? dstBasicDataBuffer = null;
		public Mesh.ExtendedVertex[]? dstExtendedDataBuffer = null;
		public int[]? dstIndexBuffer = null;

		#endregion
		#region Properties

		public bool IsValid => !IsDisposed && mesh.IsInitialized && callbackDispatchCopy != null && callbackReceiveDownloadedData != null;
		public bool IsDisposed { get; private set; } = false;

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

			if (stagingBuffers != null)
			{
				foreach (DeviceBuffer buffer in stagingBuffers)
				{
					buffer?.Dispose();
				}
			}
		}
		
		internal bool Dispatch(CommandList _blittingCmdList)
		{
			if (!IsValid) return false;
			if (_blittingCmdList == null || _blittingCmdList.IsDisposed) return false;

			return callbackDispatchCopy(_blittingCmdList, this, out stagingBuffers);
		}

		internal bool Finish()
		{
			if (!IsValid) return false;
			if (stagingBuffers == null) return false;

			// Call for data to be downloaded from staging buffers:
			if (!callbackDownloadData(this))
			{
				return false;
			}

			// If data failed to be downloaded or no destination arrays were assigned, abort:
			if (dstBasicDataBuffer != null && dstIndexBuffer != null)
			{
				return false;
			}

			// Send out downloaded data to whomever was the intended recipient:
			callbackReceiveDownloadedData(mesh, dstBasicDataBuffer!, dstExtendedDataBuffer, dstIndexBuffer!);
			return true;
		}

		#endregion
	}
}

