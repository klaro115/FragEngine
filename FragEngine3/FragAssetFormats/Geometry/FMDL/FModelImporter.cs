using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Utility;
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

	public MeshVertexDataFlags SupportedVertexData => MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	public bool CanImportSubMeshes => false;
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

		// Read basic vertex data:
		BasicVertex[]? verticesBasic = ReadGeometryDataArrayFromByteStream<BasicVertex>(
			reader,
			fileHeader.vertexCount,
			BasicVertex.byteSize,
			fileStartPosition,
			fileHeader.verticesBasic);

		if (verticesBasic is null || verticesBasic.Length == 0)
		{
			_importCtx.Logger.LogError("FMDL 3D model file does not contain any basic vertex data!");
			_outSurfaceData = null;
			return false;
		}

		// Read extended vertex data:
		ExtendedVertex[]? verticesExt = ReadGeometryDataArrayFromByteStream<ExtendedVertex>(
			reader,
			fileHeader.vertexCount,
			ExtendedVertex.byteSize,
			fileStartPosition,
			fileHeader.verticesExt);

		/*
		IndexedWeightedVertex[]? verticesBlend = ReadGeometryDataArrayFromByteStream<IndexedWeightedVertex>(
			reader,
			fileHeader.vertexCount,
			IndexedWeightedVertex.byteSize,
			fileStartPosition,
			fileHeader.verticesBlend);
		IndexedWeightedVertex[]? verticesAnim = ReadGeometryDataArrayFromByteStream<IndexedWeightedVertex>(
			reader,
			fileHeader.vertexCount,
			IndexedWeightedVertex.byteSize,
			fileStartPosition,
			fileHeader.verticesAnim);
		*/

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
			verticesBasic = verticesBasic,
			verticesExt = verticesExt,

			indices16 = indices16,
			indices32 = indices32,
		};

		bool isValid = _outSurfaceData.IsValid;
		return isValid;
	}

	private unsafe T[]? ReadGeometryDataArrayFromByteStream<T>(
		BinaryReader _reader,
		uint _dataCount,
		uint _dataElementSize,
		long _fileStartPosition,
		FModelHeader.DataBlock _dataBlock) where T : unmanaged
	{
		int expectedDataSize = (int)(_dataElementSize * _dataCount);
		if (_dataBlock.IsEmpty || _dataBlock.byteSize < expectedDataSize)
		{
			return null;
		}

		long dataBlockStartPosition = _fileStartPosition + _dataBlock.offset;
		_reader.JumpToPosition(dataBlockStartPosition);

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
