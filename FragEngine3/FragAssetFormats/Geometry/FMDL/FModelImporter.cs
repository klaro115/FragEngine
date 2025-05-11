using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Utility;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragAssetFormats.Geometry.FMDL;

/// <summary>
/// Importer for the engine's native FMDL 3D file format.
/// </summary>
public sealed class FModelImporter : IModelImporter
{
	#region Fields

	private static readonly string[] supportedFormatExtensions = [ ".fmdl" ];

	#endregion
	#region Properties

	// GEOMETRY SUPPORT:
	
	public MeshVertexDataFlags SupportedVertexData => MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	public bool Supports16BitIndices => true;
	public bool Supports32BitIndices => true;
	public bool CanImportSubMeshes => false;

	// ANIMATION SUPPORT:

	public bool CanImportAnimations => false;
	public bool CanImportMaterials => false;
	public bool CanImportTextures => false;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFormatExtensions;

	public bool ImportSurfaceData(in ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKey, out MeshSurfaceData? _outSurfaceData, string? _fileExtension = null)
	{
		if (_resourceFileStream is null)
		{
			_importCtx.Logger.LogError("Cannot import FMDL model from null stream!");
			_outSurfaceData = null;
			return false;
		}
		if (!_resourceFileStream.CanRead)
		{
			_importCtx.Logger.LogError("Cannot import FMDL model from write-only stream!");
			_outSurfaceData = null;
			return false;
		}

		long fileStartPosition = _resourceFileStream.Position;
		using BinaryReader reader = new(_resourceFileStream);

		// Read file header:
		if (!FModelHeader.ReadFmdlHeader(in _importCtx, reader, out FModelHeader fileHeader))
		{
			_importCtx.Logger.LogError("Failed to read FMDL file header from resource file stream!");
			_outSurfaceData = null;
			return false;
		}

		// Read vertex data:
		BasicVertex[]? verticesBasic = null;
		ExtendedVertex[]? verticesExt = null;
		if (fileHeader.isVertexDataCompressed)
		{
			// Compressed:
			if (!ReadCompressedVertexData(in _importCtx, in fileHeader, _resourceFileStream, fileStartPosition, out verticesBasic, out verticesExt))
			{
				_outSurfaceData = null;
				return false;
			}
		}
		else
		{
			// Uncompressed:
			if (!ReadUncompressedVertexData(in _importCtx, in fileHeader, reader, fileStartPosition, out verticesBasic, out verticesExt))
			{
				_outSurfaceData = null;
				return false;
			}
		}

		// Read index data:
		uint indexCount = fileHeader.triangleCount * 3;
		IndexFormat indexFormat = fileHeader.vertexCount > ushort.MaxValue
			? IndexFormat.UInt32
			: IndexFormat.UInt16;

		ushort[]? indices16 = null;
		int[]? indices32 = null;

		if (indexFormat == IndexFormat.UInt32)
		{
			indices32 = ReadGeometryDataArrayFromByteStream<int>(
				reader,
				indexCount,
				sizeof(int),
				fileStartPosition,
				fileHeader.indices);
		}
		else
		{
			indices16 = ReadGeometryDataArrayFromByteStream<ushort>(
				reader,
				indexCount,
				sizeof(ushort),
				fileStartPosition,
				fileHeader.indices);
		}

		// Assemble mesh surface data object:
		_outSurfaceData = new()
		{
			verticesBasic = verticesBasic!,
			verticesExt = verticesExt,

			indices16 = indices16,
			indices32 = indices32,
		};

		bool isValid = _outSurfaceData.IsValid;
		return isValid;
	}

	private bool ReadCompressedVertexData(
		in ImporterContext _importCtx,
		in FModelHeader _fileHeader,
		Stream _resourceFileStream,
		long _fileStartPosition,
		out BasicVertex[]? _outVerticesBasic,
		out ExtendedVertex[]? _outVerticesExt
		/* ... */)
	{
		// Calculate data sizes and offsets:
		int compressedSizeTotal = (int)_fileHeader.verticesBasic.byteSize;
		uint expectedSizeBasic = _fileHeader.vertexCount * BasicVertex.byteSize;
		uint expectedSizeExt = _fileHeader.vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData)
			? _fileHeader.vertexCount * BasicVertex.byteSize
			: 0;
		uint expectedSizeTotal = expectedSizeBasic + expectedSizeExt;
		uint expectedOffsetExt = expectedSizeBasic;

		// Try decompressing vertex data:
		_resourceFileStream.Position = _fileStartPosition + _fileHeader.verticesBasic.offset;

		using MemoryStream uncompressedStream = new((int)expectedSizeTotal);
		using DeflateStream decompressionStream = new(_resourceFileStream, CompressionMode.Decompress, true);

		try
		{
			decompressionStream.CopyTo(uncompressedStream, compressedSizeTotal);
			decompressionStream.Flush();

			// Treat decompressed stream as its own resource file stream:
			uncompressedStream.Position = 0;
			FModelHeader subFileHeader = _fileHeader with
			{
				verticesBasic = new()
				{
					offset = 0,
					byteSize = expectedSizeBasic,
				},
				verticesExt = new()
				{
					offset = expectedOffsetExt,
					byteSize = expectedSizeExt,
				},
				
			};
			using BinaryReader reader = new(uncompressedStream);

			// Read decompressed vertex data as usual:
			bool success = ReadUncompressedVertexData(in _importCtx, in subFileHeader, reader, 0, out _outVerticesBasic, out _outVerticesExt);
			return success;
		}
		catch (Exception ex)
		{
			_importCtx.Logger.LogException("Failed to decompress vertex geometry data for FMDL export!", ex);
			_outVerticesBasic = null;
			_outVerticesExt = null;
			return false;
		}
	}

	private bool ReadUncompressedVertexData(
		in ImporterContext _importCtx,
		in FModelHeader _fileHeader,
		BinaryReader _reader,
		long _fileStartPosition,
		out BasicVertex[]? _outVerticesBasic,
		out ExtendedVertex[]? _outVerticesExt
		/* ... */)
	{
		// Read basic vertex data:
		_outVerticesBasic = ReadGeometryDataArrayFromByteStream<BasicVertex>(
			_reader,
			_fileHeader.vertexCount,
			BasicVertex.byteSize,
			_fileStartPosition,
			_fileHeader.verticesBasic);

		if (_outVerticesBasic is null || _outVerticesBasic.Length == 0)
		{
			_importCtx.Logger.LogError("FMDL 3D model file does not contain any basic vertex data!");
			_outVerticesBasic = null;
			_outVerticesExt = null;
			return false;
		}

		// Read extended vertex data:
		_outVerticesExt = _fileHeader.vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData)
			? ReadGeometryDataArrayFromByteStream<ExtendedVertex>(
				_reader,
				_fileHeader.vertexCount,
				ExtendedVertex.byteSize,
				_fileStartPosition,
				_fileHeader.verticesExt)
			: null;

		/*
		// Read blend shape data:
		IndexedWeightedVertex[]? verticesBlend = fileHeader.vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes)
			? ReadGeometryDataArrayFromByteStream<IndexedWeightedVertex>(
				reader,
				fileHeader.vertexCount,
				IndexedWeightedVertex.byteSize,
				fileStartPosition,
				fileHeader.verticesBlend)
			: null;

		// Read bone animation weights:
		IndexedWeightedVertex[]? verticesAnim = fileHeader.vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations)
			? ReadGeometryDataArrayFromByteStream<IndexedWeightedVertex>(
				reader,
				fileHeader.vertexCount,
				IndexedWeightedVertex.byteSize,
				fileStartPosition,
				fileHeader.verticesAnim)
			: null;
		*/

		return true;
	}

	private unsafe T[]? ReadGeometryDataArrayFromByteStream<T>(
		BinaryReader _reader,
		uint _dataCount,
		uint _dataElementSize,
		long _fileStartPosition,
		FModelHeader.DataBlock _dataBlock) where T : unmanaged
	{
		// Check if the block contains any data:
		int expectedDataSize = (int)(_dataElementSize * _dataCount);
		if (_dataBlock.byteSize == 0 || _dataBlock.byteSize < expectedDataSize)
		{
			return null;
		}

		// Advance reader to the block's starting position:
		long dataBlockStartPosition = _fileStartPosition + _dataBlock.offset;
		_reader.JumpToPosition(dataBlockStartPosition);

		// Read contents from byte array as-is:
		byte[] dataBlockContent = _reader.ReadBytes(expectedDataSize);
		T[] dstBuffer = new T[_dataCount];

		fixed (T* pDstData = dstBuffer)
		{
			Marshal.Copy(dataBlockContent, 0, (nint)pDstData, expectedDataSize);
		}
		return dstBuffer;
	}

	public IEnumerator<string> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKeyBase, string? _fileExtension = null)
	{
		yield return _resourceKeyBase;
	}

	#endregion
}
