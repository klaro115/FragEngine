using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Utility;

namespace FragAssetFormats.Geometry.FMDL;

[Serializable]
public struct FModelHeader
{
	#region Types

	/// <summary>
	/// Structure representing the format version of an FSHA file.
	/// </summary>
	/// <param name="_packedVersion">Major and minor version parts, packed into an 8-bit byte, with 4 bits per part.</param>
	[Serializable]
	public readonly struct FormatVersion(byte _packedVersion)
	{
		public readonly byte major = (byte)((_packedVersion & 0xF0u) >> 4);
		public readonly byte minor = (byte)(_packedVersion & 0x0Fu);

		/// <summary>
		/// Gets a packed version of the version number, with 4 bits each for the major and minor parts.
		/// </summary>
		public byte PackedVersion => (byte)(major << 4 | minor);

		/// <summary>
		/// Gets the most recent version of the FSHA format supported by this importer/exporter implementation.
		/// </summary>
		public static FormatVersion Current => new(CURRENT_VERSION);
	}

	[Serializable]
	public struct DataBlock
	{
		public uint offset;
		public uint byteSize;

		public readonly bool IsEmpty => offset == 0 || byteSize == 0;

		public static DataBlock Empty => new() { offset = 0, byteSize = 0 };

		internal static DataBlock Read(BinaryReader _reader)
		{
			uint offset = _reader.ReadHexUint32();
			_reader.ReadByte();
			uint byteSize = _reader.ReadHexUint32();

			return new DataBlock()
			{
				offset = offset,
				byteSize = byteSize,
			};
		}

		internal readonly void Write(BinaryWriter _writer) // (9 bytes)
		{
			_writer.WriteUint32ToHex(offset);
			_writer.Write((byte)'_');
			_writer.WriteUint32ToHex(byteSize);
		}
	}

	#endregion
	#region Fields

	// FORMAT INFO:

	public uint magicNumbers;
	public FormatVersion formatVersion;
	public uint fileByteSize;

	// GEOMETRY INFO:

	public uint vertexCount;
	public uint triangleCount;
	public uint reserved;
	public MeshVertexDataFlags vertexDataFlags;

	// COMPRESSION INFO:

	public bool isVertexDataCompressed;
	public bool isIndexDataCompressed;

	// DATA BLOCKS:

	public DataBlock verticesBasic;
	public DataBlock verticesExt;
	public DataBlock verticesBlend;
	public DataBlock verticesAnim;
	public DataBlock indices;

	#endregion
	#region Constants

	public const ushort MINIMUM_HEADER_STRING_SIZE = 144;           // 0x90

	public const uint MINIMUM_DATA_BLOCKS_SIZE = BasicVertex.byteSize + 3 * sizeof(ushort);
	public const uint MINIMUM_FILE_SIZE = MINIMUM_HEADER_STRING_SIZE + MINIMUM_DATA_BLOCKS_SIZE;

	public const uint MAGIC_NUMBERS = ((uint)'F' << 0) | ((uint)'M' << 8) | ((uint)'D' << 16) | ((uint)'L' << 24);

	public const byte CURRENT_VERSION = 0x00 << 4 | 0x02;           // v0.2
	public const byte MINIMUM_SUPPORTED_VERSION = 0x00 << 4 | 0x02; // v0.2

	#endregion
	#region Methods

	public readonly bool CalculateTotalVertexDataSizes(out uint _outTotalDataSize, out uint _outStartOffset, out uint _outEndOffset)
	{
		_outTotalDataSize = 0;
		_outEndOffset = 0;

		// Start with basic vertex data:
		if (verticesBasic.IsEmpty)
		{
			_outStartOffset = 0;
			return false;
		}

		_outTotalDataSize = verticesBasic.byteSize;
		_outStartOffset = verticesBasic.offset;
		_outEndOffset = verticesBasic.offset + verticesBasic.byteSize;

		// Expand bounds from optional vertex data:
		if (!verticesExt.IsEmpty)
		{
			_outTotalDataSize += verticesExt.byteSize;
			_outStartOffset = Math.Min(verticesExt.offset, _outStartOffset);
			_outEndOffset = Math.Max(verticesExt.offset + verticesExt.byteSize, _outEndOffset);
		}
		if (!verticesBlend.IsEmpty)
		{
			_outTotalDataSize += verticesBlend.byteSize;
			_outStartOffset = Math.Min(verticesBlend.offset, _outStartOffset);
			_outEndOffset = Math.Max(verticesBlend.offset + verticesBlend.byteSize, _outEndOffset);
		}
		if (!verticesAnim.IsEmpty)
		{
			_outTotalDataSize += verticesAnim.byteSize;
			_outStartOffset = Math.Min(verticesAnim.offset, _outStartOffset);
			_outEndOffset = Math.Max(verticesAnim.offset + verticesAnim.byteSize, _outEndOffset);
		}

		// Check if those values operate in valid ranges:
		bool hasValidBounds = _outStartOffset >= MINIMUM_HEADER_STRING_SIZE && _outEndOffset > _outStartOffset;
		return hasValidBounds;
	}

	public readonly bool CalculateTotalIndexDataSizes(out uint _outTotalDataSize, out uint _outStartOffset, out uint _outEndOffset)
	{
		_outTotalDataSize = 0;
		_outEndOffset = 0;

		// Ensure index data is not missing:
		if (indices.IsEmpty)
		{
			_outStartOffset = 0;
			return false;
		}

		_outTotalDataSize = indices.byteSize;
		_outStartOffset = indices.offset;
		_outEndOffset = indices.offset + indices.byteSize;

		// Check if those values operate in valid ranges:
		bool hasValidBounds = _outStartOffset >= MINIMUM_HEADER_STRING_SIZE && _outEndOffset > _outStartOffset;
		return hasValidBounds;
	}

	public readonly bool CalculateUncompressedFileSize(out uint _outFileByteSize)
	{
		if (!CalculateTotalVertexDataSizes(out uint totalVertexBlocksSize, out _, out _) ||
			!CalculateTotalIndexDataSizes(out uint totalIndexBlockSize, out _, out _))
		{
			_outFileByteSize = 0;
			return false;
		}

		_outFileByteSize = MINIMUM_HEADER_STRING_SIZE + totalVertexBlocksSize + totalIndexBlockSize;
		return _outFileByteSize >= MINIMUM_FILE_SIZE;
	}

	public static bool ReadFmdlHeader(in ImporterContext _importCtx, BinaryReader _reader, out FModelHeader _outHeader)
	{
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read model data using null binary reader!");
			_outHeader = default;
			return false;
		}

		// Check magic numbers:
		uint magicNumbers = _reader.ReadUInt32();
		_reader.ReadByte();
		if (magicNumbers != MAGIC_NUMBERS)
		{
			_importCtx.Logger.LogError($"Magic numbers of file header indicate unsupported file format! ({magicNumbers:H} vs. ({MAGIC_NUMBERS:H}))");
			_outHeader = default;
			return false;
		}

		// Read version and check compatibility:
		FormatVersion version = new(_reader.ReadHexUint8());
		_reader.ReadByte();

		if (version.major > FormatVersion.Current.major)
		{
			// Report high probability of incompatibility, but try parsing it anyways:
			_importCtx.Logger.LogWarning($"Format version mismatch in FMDL 3D model file; importer may be outdated! (Model: {version}, Importer: {FormatVersion.Current})");
		}
		else if (version.PackedVersion < MINIMUM_SUPPORTED_VERSION)
		{
			// Reject outdated format versions immediately:
			_importCtx.Logger.LogError($"Major format version mismatch in FMDL 3D model file; model file version is no longer supported! (Model: {version}, Importer: {FormatVersion.Current})");
			_outHeader = default;
			return false;
		}
		else if (version.major == FormatVersion.Current.major && version.minor > FormatVersion.Current.minor)
		{
			// Report partial incompatibility, but try parsing it anyways:
			_importCtx.Logger.LogWarning($"Minor format version mismatch in FMDL 3D model file. (Model: {version}, Importer: {FormatVersion.Current})");
		}

		// Read and verify file size:
		uint fileByteSize = _reader.ReadHexUint32();
		_reader.ReadByte();

		if (fileByteSize < MINIMUM_FILE_SIZE)
		{
			_importCtx.Logger.LogError($"Invalid file size in FMDL 3D model file; total byte size is too low! ({fileByteSize} < {MINIMUM_FILE_SIZE})");
			_outHeader = default;
			return false;
		}

		// Read geometry info:
		uint vertexCount = _reader.ReadHexUint32();
		_reader.ReadByte();
		uint triangleCount = _reader.ReadHexUint32();
		_reader.ReadByte();
		uint reserved = _reader.ReadHexUint32();
		_reader.ReadByte();

		// Read vertex data flags:
		MeshVertexDataFlags vertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
		_reader.ReadByte(); // 'V'
		if (ReadBoolean01(_reader)) vertexDataFlags |= MeshVertexDataFlags.ExtendedSurfaceData;
		if (ReadBoolean01(_reader)) vertexDataFlags |= MeshVertexDataFlags.BlendShapes;
		if (ReadBoolean01(_reader)) vertexDataFlags |= MeshVertexDataFlags.Animations;
		_reader.ReadByte();

		// Read compression info:
		bool isVertexDataCompressed = ReadBoolean01(_reader);
		bool isIndexDataCompressed = ReadBoolean01(_reader);
		_reader.ReadByte(); // '\r'
		_reader.ReadByte(); // '\n'

		_outHeader = new()
		{
			magicNumbers = magicNumbers,
			formatVersion = version,
			fileByteSize = fileByteSize,

			vertexCount = vertexCount,
			triangleCount = triangleCount,
			reserved = reserved,
			vertexDataFlags = vertexDataFlags,

			isVertexDataCompressed = isVertexDataCompressed,
			isIndexDataCompressed = isIndexDataCompressed,
		};

		// Read vertex blocks:
		DataBlock verticesBasic = DataBlock.Read(_reader);
		if (verticesBasic.offset < MINIMUM_HEADER_STRING_SIZE)
		{
			_importCtx.Logger.LogError("Starting offset of vertex data block overlaps with FMDL format header! Aborting import...");
			return false;
		}
		if (verticesBasic.byteSize == 0)
		{
			_importCtx.Logger.LogError("Basic vertex data block of FMDL file is empty; geometry data appears to be invalid!");
			return false;
		}

		_outHeader.verticesBasic = verticesBasic;
		_reader.ReadByte();
		_outHeader.verticesExt = DataBlock.Read(_reader);
		_reader.ReadByte();
		_outHeader.verticesBlend = DataBlock.Read(_reader);
		_reader.ReadByte();
		_outHeader.verticesAnim = DataBlock.Read(_reader);
		_reader.ReadByte();

		// Read index block:
		_outHeader.indices = DataBlock.Read(_reader);

		// Perform rudimentary boundary checks of vertex and index data blocks:
		if (!_outHeader.CalculateTotalVertexDataSizes(out _, out uint vertexStartOffset, out uint vertexEndOffset))
		{
			_importCtx.Logger.LogError("Offsets and sizes of vertex data in FMDL file are invalid!");
			return false;
		}

		if (!_outHeader.CalculateTotalIndexDataSizes(out _, out uint indexStartOffset, out uint indexEndOffset))
		{
			_importCtx.Logger.LogError("Offsets and sizes of index data in FMDL file are invalid!");
			return false;
		}

		bool areVertexAndIndexOverlapping = !(vertexStartOffset >= indexEndOffset || vertexEndOffset <= indexStartOffset);
		if (areVertexAndIndexOverlapping)
		{
			_importCtx.Logger.LogError("Offsets and sizes of vertex and index data in FMDL file are overlapping!");
			return false;
		}

		return true;
	}

	public readonly bool WriteFmdlHeader(in ImporterContext _exportCtx, BinaryWriter _writer)
	{
		if (_writer is null)
		{
			_exportCtx.Logger.LogError("Cannot write FMDL file header to null binary writer!");
			return false;
		}

		// Format info: (17 bytes)
		_writer.Write(MAGIC_NUMBERS);
		_writer.Write((byte)'_');

		_writer.WriteUint8ToHex(CURRENT_VERSION);
		_writer.Write((byte)'_');

		_writer.WriteUint32ToHex(fileByteSize);
		_writer.Write((byte)'_');

		// Geometry info: (27 bytes)
		_writer.WriteUint32ToHex(vertexCount);
		_writer.Write((byte)'_');
		_writer.WriteUint32ToHex(triangleCount);
		_writer.Write((byte)'_');
		_writer.WriteUint32ToHex(reserved);
		_writer.Write((byte)'_');

		// Vertex data flags: (5 bytes)
		_writer.Write((byte)'V');
		WriteBoolean01(_writer, vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData));
		WriteBoolean01(_writer, vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes));
		WriteBoolean01(_writer, vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations));
		_writer.Write((byte)'_');

		// Compression info: (4 bytes)
		WriteBoolean01(_writer, isVertexDataCompressed);
		WriteBoolean01(_writer, isIndexDataCompressed);

		_writer.Write((byte)'\r');
		_writer.Write((byte)'\n');

		// Data blocks: (71 bytes)
		verticesBasic.Write(_writer);
		_writer.Write((byte)'_');
		verticesExt.Write(_writer);
		_writer.Write((byte)'_');
		verticesBlend.Write(_writer);
		_writer.Write((byte)'_');
		verticesAnim.Write(_writer);
		_writer.Write((byte)'_');
		indices.Write(_writer);

		// Header end: (2 bytes)
		_writer.Write((byte)'\r');
		_writer.Write((byte)'\n');
		
		return true;
	}

	private static void WriteBoolean01(BinaryWriter _writer, bool _value)
	{
		_writer.Write(_value ? (byte)'1' : (byte)'0');
	}

	private static bool ReadBoolean01(BinaryReader _reader)
	{
		return _reader.ReadByte() != (byte)'0';
	}

	#endregion
}
