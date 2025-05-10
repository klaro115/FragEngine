using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Export;
using FragEngine3.Graphics.Resources.Import;
//using System.IO.Compression;
using System.Runtime.InteropServices;

namespace FragAssetFormats.Geometry.FMDL;

/// <summary>
/// Exporter for the engine's native FMDL 3D file format.
/// </summary>
public sealed class FModelExporter : IModelExporter
{
	#region Fields

	private static readonly string[] supportedFormatExtensions = [ ".fmdl" ];

	#endregion
	#region Properties

	// GEOMETRY SUPPORT:

	public MeshVertexDataFlags SupportedVertexData => MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	public bool Supports16BitIndices => true;
	public bool Supports32BitIndices => true;

	// ANIMATION SUPPORT:

	public bool CanExportBlendTargets => false;
	public bool CanExportAnimations => false;
	public bool CanExportMaterials => false;
	public bool CanExportTextures => false;

	#endregion
	#region Constants

	private const CompressedDataFlags geometryCompressionFlags = CompressedDataFlags.Geometry_VertexData | CompressedDataFlags.Geometry_IndexData;
	private const int minGeometrySizeForCompression = 2048;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFormatExtensions;

	public bool ExportModelData(in ImporterContext _exportCtx, MeshSurfaceData _surfaceData, Stream _outputResourceStream)
	{
		if (_exportCtx is null)
		{
			Console.WriteLine("Error! Cannot write model data using null export context!");
			return false;
		}
		if (_outputResourceStream is null)
		{
			_exportCtx.Logger.LogError("Cannot write model data to null binary writer!");
			return false;
		}
		if (!_outputResourceStream.CanWrite)
		{
			_exportCtx.Logger.LogError("Cannot write model data to read-only stream!");
			return false;
		}

		CalculateUncompressedGeometrySize(_surfaceData, out int actualVertexCount, out int totalUncompressedGeometrySize);

		/*
		MemoryStream? compressedGeometryStream = null;
		byte[]? intermediateBuffer = null;
		if (totalUncompressedGeometrySize >= minGeometrySizeForCompression && (_exportCtx.PreferDataCompression & geometryCompressionFlags) != 0)
		{
			intermediateBuffer = new byte[totalUncompressedGeometrySize];
			compressedGeometryStream = new(totalUncompressedGeometrySize);

			//using DeflateStream compressionStream = new(intermediateStream, CompressionMode.Compress, false);

			//TODO
		}
		*/

		// Write geometry data to byte arrays:
		byte[] vertexDataBasic = WriteGeometryDataArrayToByteBuffer(_surfaceData.verticesBasic, actualVertexCount, (int)BasicVertex.byteSize)!;
		byte[]? vertexDataExt = WriteGeometryDataArrayToByteBuffer(_surfaceData.verticesExt, actualVertexCount, (int)ExtendedVertex.byteSize);
		byte[]? indexData = _surfaceData.IndexFormat == Veldrid.IndexFormat.UInt32
			? WriteGeometryDataArrayToByteBuffer(_surfaceData.indices32, actualVertexCount, sizeof(int))
			: WriteGeometryDataArrayToByteBuffer(_surfaceData.indices16, actualVertexCount, sizeof(ushort));

		if (vertexDataBasic is null || actualVertexCount == 0)
		{
			_exportCtx.Logger.LogError("Cannot write model data with zero vertices!");
			return false;
		}

		//TODO: Insert data compression here.

		// Calculate block sizes and offsets:
		uint vertexBasicOffset = FModelHeader.MINIMUM_HEADER_STRING_SIZE;
		uint vertexBasicByteSize = (uint)vertexDataBasic.Length;
		uint vertexExtOffset = vertexBasicOffset + vertexBasicByteSize;
		uint vertexExtByteSize = vertexDataExt is not null ? (uint)vertexDataExt.Length : 0;
		uint indexOffset = vertexExtOffset + vertexExtByteSize;
		uint indexByteSize = indexData is not null ? (uint)indexData.Length : 0;

		// Assemble header:
		FModelHeader fileHeader = new()
		{
			// Format info:
			magicNumbers = FModelHeader.MAGIC_NUMBERS,
			formatVersion = FModelHeader.FormatVersion.Current,
			fileByteSize = 0u,

			// Geometry info:
			vertexCount = (uint)actualVertexCount,
			triangleCount = (uint)_surfaceData.TriangleCount,
			reserved = 0u,

			// Compression info:
			isVertexDataCompressed = false,
			isIndexDataCompressed = false,

			// Data blocks:
			verticesBasic = new()
			{
				offset = vertexBasicOffset,
				byteSize = vertexBasicByteSize,
			},
			verticesExt = new()
			{
				offset = vertexExtOffset,
				byteSize = vertexExtByteSize,
			},
			verticesBlend = FModelHeader.DataBlock.Empty,
			verticesAnim = FModelHeader.DataBlock.Empty,
			indices = new()
			{
				offset = indexOffset,
				byteSize = indexByteSize,
			}
		};

		if (!fileHeader.CalculateUncompressedFileSize(out fileHeader.fileByteSize))
		{
			_exportCtx.Logger.LogError("Failed to estimate uncompressed size of FMDL 3D model file!");
			return false;
		}

		// Write header to file:
		using BinaryWriter writer = new(_outputResourceStream);

		if (!fileHeader.WriteFmdlHeader(in _exportCtx, writer))
		{
			_exportCtx.Logger.LogError("Failed to write format header of FMDL 3D model to file!");
			return false;
		}

		// Write block data to file.
		writer.Write(vertexDataBasic);
		if (vertexDataExt is not null)
		{
			writer.Write(vertexDataExt);
		}
		if (indexData is not null)
		{
			writer.Write(indexData);
		}

		return true;
	}

	private static void CalculateUncompressedGeometrySize(MeshSurfaceData _surfaceData, out int _outActualVertexCount, out int _outTotalGeometrySize)
	{
		// Vertices:
		_outActualVertexCount = _surfaceData.VertexCount;
		int totalPerVertexSize = (int)BasicVertex.byteSize;

		if (_surfaceData.HasExtendedVertexData)
		{
			_outActualVertexCount = Math.Min(_surfaceData.verticesExt!.Length, _outActualVertexCount);
			totalPerVertexSize += (int)ExtendedVertex.byteSize;
		}
		int totalVertexSize = _outActualVertexCount * totalPerVertexSize;

		// Indices:
		int indexCount = _surfaceData.IndexCount;
		int totalPerIndexSize = _surfaceData.IndexFormat == Veldrid.IndexFormat.UInt32 ? sizeof(int) : sizeof(ushort);
		int totalIndexSize = indexCount * totalPerIndexSize;

		_outTotalGeometrySize = totalVertexSize + totalIndexSize;
	}

	private unsafe byte[]? WriteGeometryDataArrayToByteBuffer<T>(T[]? _data, int _dataCount, int _dataElementSize) where T : unmanaged
	{
		if (_data is null || _data.Length == 0)
		{
			return null;
		}

		int dataByteSize = _dataCount * _dataElementSize;
		byte[] dstBuffer = new byte[dataByteSize];

		fixed (T* pData = _data)
		{
			Marshal.Copy((nint)pData, dstBuffer, 0, dataByteSize);
		}
		return dstBuffer;
	}

	public IEnumerator<string> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKeyBase, string? _fileExtension = null)
	{
		yield return _resourceKeyBase;
	}

	#endregion
}
