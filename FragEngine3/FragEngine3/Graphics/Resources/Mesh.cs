using System.Numerics;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources;

public sealed class Mesh : Resource
{
	#region Constructors

	public Mesh(ResourceHandle _handle, GraphicsCore? _graphicsCore) : base(_handle)
	{
		graphicsCore = _graphicsCore ?? _handle.resourceManager.engine.GraphicsSystem.graphicsCore;
	}
	public Mesh(string _resourceKey, Engine _engine, out ResourceHandle _outHandle) : base(_resourceKey, _engine)
	{
		_outHandle = new(this);
		resourceManager.AddResource(_outHandle);
		graphicsCore = _engine.GraphicsSystem.graphicsCore;
	}

	~Mesh()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever any of the vertex or index buffer objects have been swapped, aka when new buffers were created.
	/// </summary>
	public event Action<Mesh>? OnGeometryBuffersChanged = null;
	/// <summary>
	/// Event that is triggered whenever the geometry data in vertex or index buffers has been updated, aka when new values have been uploaded to GPU memory.
	/// </summary>
	public event Action<Mesh>? OnGeometryDataChanged = null;

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;

	// Vertices:
	private DeviceBuffer[] vertexBuffers = [];
	private DeviceBuffer? bufVerticesBasic = null;
	private DeviceBuffer? bufVerticesExt = null;
	private DeviceBuffer? bufVerticesBlend = null;
	private DeviceBuffer? bufVerticesAnim = null;

	private MeshVertexDataFlags areVerticesDirtyFlags = MeshVertexDataFlags.ALL;
	private BasicVertex[]? pendingVerticesBasic = null;
	private ExtendedVertex[]? pendingVerticesExt = null;
	private IndexedWeightedVertex[]? pendingVerticesBlend = null;
	private IndexedWeightedVertex[]? pendingVerticesAnim = null;

	// Indices:
	private DeviceBuffer? bufIndices = null;

	private bool areIndicesDirty = true;
	private ushort[]? pendingIndices16 = null;
	private int[]? pendingIndices32 = null;

	private readonly object lockObj = new();

	#endregion
	#region Properties

	public override ResourceType ResourceType => ResourceType.Model;

	public bool IsInitialized => !IsDisposed && bufVerticesBasic is not null && bufIndices is not null;
	public bool IsDirty => areVerticesDirtyFlags != 0 || areIndicesDirty;

	public int VertexBufferCount => vertexBuffers.Length;
	public MeshVertexDataFlags VertexDataFlags { get; private set; } = MeshVertexDataFlags.BasicSurfaceData;
	public IndexFormat IndexFormat { get; private set; } = IndexFormat.UInt16;

	public uint VertexCount { get; private set; } = 0;
	public uint IndexCount { get; private set; } = 0;
	public uint TriangleCount { get; private set; } = 0;

	public float BoundingRadius { get; private set; } = 1.0f;

	private Logger Logger => graphicsCore.graphicsSystem.engine.Logger;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		vertexBuffers = [];
		bufVerticesBasic?.Dispose();
		bufVerticesExt?.Dispose();
		bufVerticesBlend?.Dispose();
		bufVerticesAnim?.Dispose();

		bufIndices?.Dispose();
	}

	public bool SetVertexData(
		IList<BasicVertex> _verticesBasic,
		IList<ExtendedVertex>? _verticesExt,
		IList<IndexedWeightedVertex>? _verticesBlend = null,
		IList<IndexedWeightedVertex>? _verticesAnim = null,
		int _vertexCount = -1)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot set vertex data of disposed mesh!");
			return false;
		}
		if (_verticesBasic is null)
		{
			Logger.LogError("Mesh's basic vertex data may not be null!");
			return false;
		}

		pendingVerticesBasic = null;
		pendingVerticesExt = null;
		pendingVerticesBlend = null;
		pendingVerticesAnim = null;

		// Determine vertex count and number of buffers:
		MeshVertexDataFlags newVertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
		int newVertexCount = _vertexCount >= 0
			? Math.Min(_verticesBasic.Count, _vertexCount)
			: _verticesBasic.Count;
		int newVertexBufferCount = 1;

		CheckVertexData(_verticesExt, MeshVertexDataFlags.ExtendedSurfaceData);
		CheckVertexData(_verticesBlend, MeshVertexDataFlags.BlendShapes);
		CheckVertexData(_verticesAnim, MeshVertexDataFlags.Animations);

		// Recalculate bounding radius from new position data:
		float newBoundingRadiusSq = 0.0f;
		for (int i = 0; i < newVertexCount; ++i)
		{
			Vector3 position = _verticesBasic[i].position;
			float originDistSq = position.LengthSquared();
			newBoundingRadiusSq = MathF.Min(originDistSq, newBoundingRadiusSq);
		}

		bool success = true;
		bool wereBuffersChanged = false;
		int currentBufferIndex = 0;

		lock (lockObj)
		{
			// Update flags and counters:
			areVerticesDirtyFlags = newVertexDataFlags;
			VertexDataFlags = newVertexDataFlags;
			VertexCount = (uint)newVertexCount;
			BoundingRadius = MathF.Sqrt(newBoundingRadiusSq);

			if (vertexBuffers.Length != newVertexBufferCount)
			{
				vertexBuffers = new DeviceBuffer[newVertexBufferCount];
			}

			// Create or resize GPU buffers:
			RecreateVertexBuffer(ref bufVerticesBasic, MeshVertexDataFlags.BasicSurfaceData, BasicVertex.byteSize);
			RecreateVertexBuffer(ref bufVerticesExt, MeshVertexDataFlags.ExtendedSurfaceData, ExtendedVertex.byteSize);
			RecreateVertexBuffer(ref bufVerticesBlend, MeshVertexDataFlags.BlendShapes, IndexedWeightedVertex.byteSize);
			RecreateVertexBuffer(ref bufVerticesAnim, MeshVertexDataFlags.Animations, IndexedWeightedVertex.byteSize);

			// Assign pending data for just-in-time upload before the next draw call:
			pendingVerticesBasic = _verticesBasic.ToArray();
			if (VertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData)) pendingVerticesExt = _verticesExt!.ToArray();
			if (VertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes)) pendingVerticesBlend = _verticesBlend!.ToArray();
			if (VertexDataFlags.HasFlag(MeshVertexDataFlags.Animations)) pendingVerticesAnim = _verticesAnim!.ToArray();
		}

		// Notify any users of this mesh if geometry buffers have been replaced:
		if (wereBuffersChanged)
		{
			OnGeometryBuffersChanged?.Invoke(this);
		}
		return success;


		void CheckVertexData<T>(IList<T>? _vertexData, MeshVertexDataFlags _vertexDataFlag) where T : unmanaged
		{
			if (_vertexData is not null)
			{
				newVertexDataFlags |= _vertexDataFlag;
				newVertexCount = Math.Min(newVertexCount, _vertexData.Count);
				newVertexBufferCount++;
			}
		}
		bool RecreateVertexBuffer(ref DeviceBuffer? _bufVertices, MeshVertexDataFlags _vertexDataFlag, uint _elementByteSize)
		{
			if (!VertexDataFlags.HasFlag(_vertexDataFlag))
			{
				return true;
			}

			// Check if the previous buffer is still alive and large enough for the new data:
			uint totalByteSize = _elementByteSize * VertexCount;
			if (_bufVertices is null || _bufVertices.IsDisposed || _bufVertices.SizeInBytes < totalByteSize)
			{
				wereBuffersChanged = true;

				_bufVertices?.Dispose();
				_bufVertices = null;

				// Create a new buffer:
				try
				{
					BufferDescription bufferDesc = new(totalByteSize, BufferUsage.VertexBuffer);
					_bufVertices = graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_bufVertices.Name = $"BufVertex_x{newVertexCount}_{_vertexDataFlag}";

					vertexBuffers[currentBufferIndex++] = _bufVertices;
				}
				catch (Exception ex)
				{
					Logger.LogException($"Mesh '{resourceKey}' failed to create or resize vertex buffer '{_vertexDataFlag}'!", ex);
					return false;
				}
			}
			return true;
		}
	}

	public bool SetIndexData(IList<ushort> _indices16, int _indexCount = -1)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot set index data of disposed mesh!");
			return false;
		}
		if (_indices16 is null)
		{
			Logger.LogError("Mesh's index data may not be null!");
			return false;
		}

		// Determine index count and format:
		int newIndexCount = _indexCount >= 0
			? Math.Min(Math.Min(_indices16.Count, _indexCount), ushort.MaxValue)
			: Math.Min(_indices16.Count, ushort.MaxValue);

		bool wasBuffersChanged = false;

		lock(lockObj)
		{
			// Update flags and counters:
			IndexCount = (uint)newIndexCount;
			TriangleCount = IndexCount / 3;
			IndexFormat = IndexFormat.UInt16;

			// Check if the previous buffer is still alive and large enough for the new data:
			uint totalByteSize = sizeof(ushort) * IndexCount;
			if (bufIndices is null || bufIndices.IsDisposed || bufIndices.SizeInBytes < totalByteSize)
			{
				wasBuffersChanged = true;

				bufIndices?.Dispose();
				bufIndices = null;

				// Create a new buffer:
				try
				{
					BufferDescription bufferDesc = new(totalByteSize, BufferUsage.IndexBuffer);
					bufIndices = graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					bufIndices.Name = $"BufIndices_x{newIndexCount}_{IndexFormat.UInt16}";
				}
				catch (Exception ex)
				{
					Logger.LogException($"Mesh '{resourceKey}' failed to create or resize index buffer '{IndexFormat.UInt16}'!", ex);
					return false;
				}
			}

			// Assign pending data for just-in-time upload before the next draw call:
			pendingIndices16 = _indices16.ToArray();
			pendingIndices32 = null;
		}

		// Notify any users of this mesh if geometry buffers have been replaced:
		if (wasBuffersChanged)
		{
			OnGeometryBuffersChanged?.Invoke(this);
		}
		return true;
	}

	public bool SetIndexData(IList<int> _indices32, int _indexCount = -1)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot set index data of disposed mesh!");
			return false;
		}
		if (_indices32 is null)
		{
			Logger.LogError("Mesh's index data may not be null!");
			return false;
		}

		// Determine index count and format:
		int newIndexCount = _indexCount >= 0
			? Math.Min(_indices32.Count, _indexCount)
			: _indices32.Count;

		IndexFormat newIndexFormat;
		uint elementByteSize;
		if (newIndexCount < ushort.MaxValue)
		{
			newIndexFormat = IndexFormat.UInt16;
			elementByteSize = sizeof(ushort);
		}
		else
		{
			newIndexFormat = IndexFormat.UInt32;
			elementByteSize = sizeof(int);
		}

		bool wasBuffersChanged = false;

		lock (lockObj)
		{
			// Update flags and counters:
			IndexCount = (uint)newIndexCount;
			TriangleCount = IndexCount / 3;
			IndexFormat = newIndexFormat;

			// Check if the previous buffer is still alive and large enough for the new data:
			uint totalByteSize = elementByteSize * IndexCount;
			if (bufIndices is null || bufIndices.IsDisposed || bufIndices.SizeInBytes < totalByteSize)
			{
				wasBuffersChanged = true;

				bufIndices?.Dispose();
				bufIndices = null;

				// Create a new buffer:
				try
				{
					BufferDescription bufferDesc = new(totalByteSize, BufferUsage.IndexBuffer);
					bufIndices = graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					bufIndices.Name = $"BufIndices_x{newIndexCount}_{newIndexFormat}";
				}
				catch (Exception ex)
				{
					Logger.LogException($"Mesh '{resourceKey}' failed to create or resize index buffer '{newIndexFormat}'!", ex);
					return false;
				}
			}

			// Assign pending data for just-in-time upload before the next draw call:
			if (newIndexFormat == IndexFormat.UInt16)
			{
				pendingIndices16 = new ushort[newIndexCount];
				pendingIndices32 = null;
				for (int i = 0; i < newIndexCount; ++i)
				{
					pendingIndices16[i] = (ushort)_indices32[i];
				}
			}
			else
			{
				pendingIndices16 = null;
				pendingIndices32 = new int[newIndexCount];
				for (int i = 0; i < newIndexCount; ++i)
				{
					pendingIndices32[i] = _indices32[i];
				}
			}
		}

		// Notify any users of this mesh if geometry buffers have been replaced:
		if (wasBuffersChanged)
		{
			OnGeometryBuffersChanged?.Invoke(this);
		}
		return true;
	}

	public bool SetGeometry(in MeshSurfaceData _surfaceData)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot set surface data of disposed mesh!");
			return false;
		}
		if (_surfaceData is null)
		{
			Logger.LogError("Mesh's surface data may not be null!");
			return false;
		}

		// Set vertex data:
		if (!SetVertexData(_surfaceData.verticesBasic, _surfaceData.verticesExt, null, null, _surfaceData.VertexCount))
		{
			Logger.LogError($"Failed to set vertices from surface data on mesh '{resourceKey}'!");
			return false;
		}

		// Set index data:
		bool wereIndicesSet;
		if (_surfaceData.IndexFormat == IndexFormat.UInt16)
		{
			wereIndicesSet = SetIndexData(_surfaceData.indices16!, _surfaceData.IndexCount);
		}
		else
		{
			wereIndicesSet = SetIndexData(_surfaceData.indices32!, _surfaceData.IndexCount);
		}
		if (!wereIndicesSet)
		{
			Logger.LogError($"Failed to set indices of format '{_surfaceData.IndexFormat}' from surface data on mesh '{resourceKey}'!");
			return false;
		}

		return true;
	}

	public bool Prepare(out DeviceBuffer[] _outBufVertices, out DeviceBuffer _outBufIndices)
	{
		if (!IsInitialized)
		{
			Logger.LogError("Cannot prepare uninitialized or disposed mesh for rendering!");
			_outBufVertices = null!;
			_outBufIndices = null!;
			return false;
		}

		// Upload geometry data to buffers, if that hasn't happened yet:
		bool dataHasChanged = false;
		if (areVerticesDirtyFlags != 0)
		{
			if (!UploadPendingVertexData())
			{
				_outBufVertices = null!;
				_outBufIndices = null!;
				return false;
			}
			dataHasChanged = true;
		}
		if (areIndicesDirty)
		{
			if (!UploadPendingIndexData())
			{
				_outBufVertices = null!;
				_outBufIndices = null!;
				return false;
			}
			dataHasChanged = true;
		}

		// Output buffers and return success:
		_outBufVertices = vertexBuffers;
		_outBufIndices = bufIndices!;

		// Notify any users of this mesh if geometry data has been updated:
		if (dataHasChanged)
		{
			OnGeometryDataChanged?.Invoke(this);
		}
		return true;
	}

	private bool UploadPendingVertexData()
	{
		bool success = true;

		lock(lockObj)
		{
			success &= UploadDataToBuffer(bufVerticesBasic!, ref pendingVerticesBasic, MeshVertexDataFlags.BasicSurfaceData);
			success &= UploadDataToBuffer(bufVerticesExt!, ref pendingVerticesExt, MeshVertexDataFlags.ExtendedSurfaceData);
			success &= UploadDataToBuffer(bufVerticesBlend!, ref pendingVerticesBlend, MeshVertexDataFlags.BlendShapes);
			success &= UploadDataToBuffer(bufVerticesAnim!, ref pendingVerticesAnim, MeshVertexDataFlags.Animations);

			if (success)
			{
				areVerticesDirtyFlags = 0;
			}
		}
		return success;


		bool UploadDataToBuffer<T>(DeviceBuffer _bufVertexData, ref T[]? _pendingData, MeshVertexDataFlags _vertexDataFlag) where T : unmanaged
		{
			if (areVerticesDirtyFlags.HasFlag(_vertexDataFlag) && _pendingData is not null)
			{
				try
				{
					graphicsCore.Device.UpdateBuffer(_bufVertexData, 0, _pendingData);
				}
				catch (Exception ex)
				{
					Logger.LogException($"Mesh '{resourceKey}' failed to upload pending vertex data '{_vertexDataFlag}' to vertex buffer on GPU!", ex);
					return false;
				}
			}
			_pendingData = null;
			return true;
		}
	}

	private bool UploadPendingIndexData()
	{
		lock(lockObj)
		{
			try
			{
				if (IndexFormat == IndexFormat.UInt16 && pendingIndices16 is not null)
				{
					graphicsCore.Device.UpdateBuffer(bufIndices, 0, pendingIndices16);
				}
				else if (IndexFormat == IndexFormat.UInt32 && pendingIndices32 is not null)
				{
					graphicsCore.Device.UpdateBuffer(bufIndices, 0, pendingIndices32);
				}
			}
			catch (Exception ex)
			{
				Logger.LogException($"Mesh '{resourceKey}' failed to upload pending index data ({IndexFormat}) to vertex buffer on GPU!", ex);
				return false;
			}

			pendingIndices16 = null;
			pendingIndices32 = null;
			areIndicesDirty = false;
		}
		return true;
	}

	public bool RequestGeometryDownload(AsyncGeometryDownloadRequest.CallbackReceiveDownloadedData _downloadCallback)
	{
		if (!IsInitialized)
		{
			Logger.LogError("Cannot download geometry data from uninitialized or disposed mesh!");
			return false;
		}
		if (_downloadCallback is null)
		{
			Logger.LogError("Cannot download mesh geometry data using null download callback!");
			return false;
		}

		AsyncGeometryDownloadRequest request = new(
			this,
			CallbackDispatchCopyForGeometryDownload,
			CallbackDownloadDataForGeometryDownload,
			_downloadCallback)
		{
			dstSurfaceData = new MeshSurfaceData()
			{
				verticesBasic = new BasicVertex[VertexCount],
				verticesExt = bufVerticesExt is not null ? new ExtendedVertex[VertexCount] : null,
				indices16 = IndexFormat == IndexFormat.UInt16 ? new ushort[IndexCount] : null,
				indices32 = IndexFormat == IndexFormat.UInt32 ? new int[IndexCount] : null,
			},
			dstBlendShapeData = bufVerticesBlend is not null ? new IndexedWeightedVertex[VertexCount] : null,
			dstAnimationData = bufVerticesAnim is not null ? new IndexedWeightedVertex[VertexCount] : null,
		};

		return graphicsCore.ScheduleAsyncGeometryDownload(request);
	}

	private bool CallbackDispatchCopyForGeometryDownload(CommandList _blittingCmdList, AsyncGeometryDownloadRequest _request, out DeviceBuffer[] _outStagingBuffers, out MeshVertexDataFlags[] _outStagingBufferDataTypes)
	{
		if (!IsInitialized)
		{
			Logger.LogError("Cannot dispatch copy for geometry download from uninitialized or disposed mesh!");
			_outStagingBuffers = [];
			_outStagingBufferDataTypes = [];
			return true;
		}
		if (_blittingCmdList is null || _blittingCmdList.IsDisposed)
		{
			Logger.LogError("Cannot download mesh geometry data using null or disposed command list!");
			_outStagingBuffers = [];
			_outStagingBufferDataTypes = [];
			return false;
		}
		if (!_request.IsValid)
		{
			Logger.LogError("Mesh geometry data download request is invalid!");
			_outStagingBuffers = [];
			_outStagingBufferDataTypes = [];
			return false;
		}

		bool success = true;

		int stagingBufferIdx = 0;
		int stagingBufferCount = VertexBufferCount + 1;
		DeviceBuffer[] stagingBuffers = new DeviceBuffer[stagingBufferCount];
		MeshVertexDataFlags[] dataTypes = new MeshVertexDataFlags[stagingBufferCount];

		// Vertex buffers:
		success &= CreateStagingBufferAndScheduleCopy(bufVerticesBasic, _request.dstSurfaceData?.verticesBasic, MeshVertexDataFlags.BasicSurfaceData);
		success &= CreateStagingBufferAndScheduleCopy(bufVerticesExt, _request.dstSurfaceData?.verticesExt, MeshVertexDataFlags.ExtendedSurfaceData);
		success &= CreateStagingBufferAndScheduleCopy(bufVerticesBlend, _request.dstBlendShapeData, MeshVertexDataFlags.BlendShapes);
		success &= CreateStagingBufferAndScheduleCopy(bufVerticesAnim, _request.dstAnimationData, MeshVertexDataFlags.Animations);

		// Index buffer:
		if (IndexFormat == IndexFormat.UInt16)
		{
			success &= CreateStagingBufferAndScheduleCopy(bufIndices, _request.dstSurfaceData?.indices16, 0);
		}
		else
		{
			success &= CreateStagingBufferAndScheduleCopy(bufIndices, _request.dstSurfaceData?.indices32, 0);
		}
		
		_outStagingBuffers = stagingBuffers;
		_outStagingBufferDataTypes = dataTypes;
		return success;


		// Local helper method for creating staging buffer, and to issue the copy command:
		bool CreateStagingBufferAndScheduleCopy(DeviceBuffer? _copyFrom, Array? _dstDataBuffer, MeshVertexDataFlags _dataFlag)
		{
			if (_copyFrom is null || _dstDataBuffer is null)
			{
				return true;
			}

			BufferDescription bufferDesc = new(_copyFrom.SizeInBytes, BufferUsage.Staging);

			try
			{
				// Create staging buffer:
				DeviceBuffer stagingBuffer = graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
				stagingBuffer.Name = $"BufGeometryDownload_{resourceKey}_V{(int)_dataFlag}";

				stagingBuffers[stagingBufferIdx] = stagingBuffer;
				dataTypes[stagingBufferIdx] = _dataFlag;
				stagingBufferIdx++;

				// Schedule copy via command list:
				_blittingCmdList.CopyBuffer(_copyFrom, 0u, stagingBuffer, 0u, bufferDesc.SizeInBytes);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create staging buffer for downloading geometry data from buffer '{_copyFrom.Name}'!", ex);
				return false;
			}
		}
	}

	private bool CallbackDownloadDataForGeometryDownload(AsyncGeometryDownloadRequest _request)
	{
		if (!_request.IsValid)
		{
			Logger.LogError("Mesh geometry data download request is invalid!");
			return false;
		}
		if (_request.StagingBuffers is null || _request.StagingBufferDataTypes is null)
		{
			Logger.LogError("Mesh geometry data download request has no staging buffers to download from!");
			return false;
		}

		bool success = true;

		// Vertex data:
		success &= TryMapAndDownloadData(_request.dstSurfaceData?.verticesBasic, BasicVertex.byteSize, MeshVertexDataFlags.BasicSurfaceData);
		success &= TryMapAndDownloadData(_request.dstSurfaceData?.verticesExt, BasicVertex.byteSize, MeshVertexDataFlags.ExtendedSurfaceData);
		success &= TryMapAndDownloadData(_request.dstBlendShapeData, IndexedWeightedVertex.byteSize, MeshVertexDataFlags.BlendShapes);
		success &= TryMapAndDownloadData(_request.dstAnimationData, IndexedWeightedVertex.byteSize, MeshVertexDataFlags.Animations);

		// Index data:
		if (IndexFormat == IndexFormat.UInt16)
		{
			success &= TryMapAndDownloadData(_request.dstSurfaceData?.indices16, sizeof(ushort), 0);
		}
		else
		{
			success &= TryMapAndDownloadData(_request.dstSurfaceData?.indices32, sizeof(int), 0);
		}

		return success;


		// Local helper method for checking if a specific type of geometry data is available for download, and than map and copy data from staging buffer:
		bool TryMapAndDownloadData<T>(T[]? _dstDataBuffer, uint _elementByteSize, MeshVertexDataFlags _dataFlag) where T : unmanaged
		{
			// Check if a staging buffer of that data type exists:
			int stagingBufferIdx = Array.IndexOf(_request.StagingBufferDataTypes, _dataFlag);
			if (stagingBufferIdx < 0)
			{
				return true;
			}

			// Get staging buffer, abort quietly if no destination exists:
			DeviceBuffer stagingBuffer = _request.StagingBuffers[stagingBufferIdx];

			if (_dstDataBuffer is null)
			{
				return true;
			}

			uint stagingBufferElementCount = stagingBuffer.SizeInBytes / _elementByteSize;
			uint downloadElementCount = Math.Min((uint)_dstDataBuffer.Length, stagingBufferElementCount);

			try
			{
				var mappedData = graphicsCore.Device.Map<T>(stagingBuffer, MapMode.Read);
				for (uint i = 0; i < downloadElementCount; ++i)
				{
					_dstDataBuffer[i] = mappedData[i];
				}
				graphicsCore.Device.Unmap(stagingBuffer);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to map and download mesh geometry data of type '{typeof(T).Name}'", ex);
				return false;
			}
		}
	}

	public override IEnumerator<ResourceHandle> GetResourceDependencies()
	{
		if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
		{
			yield return handle;
		}
	}

	#endregion
}
