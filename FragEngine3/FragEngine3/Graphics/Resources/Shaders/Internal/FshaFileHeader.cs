using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Utility;

namespace FragEngine3.Graphics.Resources.Shaders.Internal;

/// <summary>
/// Object representing the deserialized contents of an FSHA file header.<para/>
/// Note: The header at the start of an FSHA file starts with the magic number "FSHA", which is ommitted in this representation,
/// but required for identifying the format during import.
/// </summary>
[Serializable]
public sealed class FshaFileHeader
{
	#region Types

	/// <summary>
	/// Structure representing the format version of an FSHA file.
	/// </summary>
	/// <param name="_packedVersion">Major and minor version parts, packed into an 8-bit byte, with 4 bits per part.</param>
	[Serializable]
	public readonly struct Version(byte _packedVersion)
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
		public static Version Current => new(CURRENT_VERSION);
	}

	#endregion
	#region Properties

	// FILE DATA:

	public required ushort HeaderSize { get; init; } = MINIMUM_HEADER_SIZE;
	public required Version FileVersion { get; init; } = Version.Current;

	// DESCRIPTION:

	public required ushort JsonOffset { get; init; }
	public required ushort JsonSize { get; init; }

	// SOURCE CODE:

	public ushort SourceCodeOffset { get; init; }
	public ushort SourceCodeSize { get; init; }

	// COMPILED DATA:

	public byte CompiledDataBlockCount { get; init; }
	public uint CompiledDataOffset { get; init; }
	public uint CompiledDataSize { get; init; }

	#endregion
	#region Constants

	public const ushort MINIMUM_HEADER_SIZE = 55;           // 0x37

	public const uint MAGIC_NUMBERS = (byte)'F' << 0 | (byte)'S' << 8 | (byte)'H' << 16 | (byte)'A' << 24;

	public const byte CURRENT_VERSION = 0x00 << 4 | 0x02; // v0.2

	#endregion
	#region Methods

	/// <summary>
	/// Reads and parses the header of an FSHA shader file.
	/// </summary>
	/// <param name="_importCtx">A context object providing logging functionality.</param>
	/// <param name="_reader">A binary reader for reading the ASCII-encoded file header from stream.</param>
	/// <param name="_outHeader">Outputs a fully parsed header object, or null on failure.</param>
	/// <returns>True if the header was read and parsed successfully, false otherwise.</returns>
	public static bool ReadFshaHeader(in ImporterContext _importCtx, BinaryReader _reader, out FshaFileHeader _outHeader)
	{
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data");
			_outHeader = null!;
			return false;
		}

		uint magicNumbers = _reader.ReadUInt32();
		_reader.ReadByte();
		if (magicNumbers != MAGIC_NUMBERS)
		{
			_importCtx.Logger.LogError($"Magic numbers of file header indicate unsupported file format! ({magicNumbers:H} vs. ({MAGIC_NUMBERS:H}))");
			_outHeader = null!;
			return false;
		}

		Version version = new(_reader.ReadHexUint8());
		_reader.ReadByte();

		ushort jsonOffset = _reader.ReadHexUint16();
		_reader.ReadByte();
		if (jsonOffset < MINIMUM_HEADER_SIZE)
		{
			_importCtx.Logger.LogError($"Invalid shader data header size! ({jsonOffset} bytes vs. {MINIMUM_HEADER_SIZE} bytes)");
			_outHeader = null!;
			return false;
		}
		ushort jsonSize = _reader.ReadHexUint16();
		_reader.ReadByte();

		ushort sourceCodeOffset = _reader.ReadHexUint16();
		_reader.ReadByte();
		ushort sourceCodeSize = _reader.ReadHexUint16();
		_reader.ReadByte();

		byte compiledDateBlockCount = _reader.ReadHexUint8();
		_reader.ReadByte();

		uint compiledDataOffset = _reader.ReadHexUint32();
		_reader.ReadByte();
		uint compiledDataSize = _reader.ReadHexUint32();

		_reader.ReadByte(); // '\r'
		_reader.ReadByte(); // '\n'

		_outHeader = new FshaFileHeader()
		{
			FileVersion = version,
			HeaderSize = jsonOffset,

			JsonOffset = jsonOffset,
			JsonSize = jsonSize,

			SourceCodeOffset = sourceCodeOffset,
			SourceCodeSize = sourceCodeSize,

			CompiledDataBlockCount = compiledDateBlockCount,
			CompiledDataOffset = compiledDataOffset,
			CompiledDataSize = compiledDataSize,
		};
		return true;
	}

	/// <summary>
	/// Writes the header to stream as an ASCII-encoded string of a fixed length.<para/>
	/// All numeric values are written as hexadecimal digits, separated by underscores.
	/// </summary>
	/// <param name="_importCtx">A context object providing logging functionality.</param>
	/// <param name="_writer">A binary writer for writing the ASCII-encoded file header to stream.</param>
	/// <returns>True if the header was written successfully, false otherwise.</returns>
	public bool WriteFshaHeader(in ImporterContext _importCtx, BinaryWriter _writer)
	{
		if (_writer is null)
		{
			_importCtx.Logger.LogError("Cannot write shader data header to null binary writer!");
			return false;
		}

		_writer.Write(MAGIC_NUMBERS);
		_writer.Write((byte)'_');

		_writer.WriteUint8ToHex(FileVersion.PackedVersion);
		_writer.Write((byte)'_');

		_writer.WriteUint16ToHex(JsonOffset);
		_writer.Write((byte)'_');
		_writer.WriteUint16ToHex(JsonSize);
		_writer.Write((byte)'_');

		_writer.WriteUint16ToHex(SourceCodeOffset);
		_writer.Write((byte)'_');
		_writer.WriteUint16ToHex(SourceCodeSize);
		_writer.Write((byte)'_');

		_writer.WriteUint8ToHex(CompiledDataBlockCount);
		_writer.Write((byte)'_');
		_writer.WriteUint32ToHex(CompiledDataOffset);
		_writer.Write((byte)'_');
		_writer.WriteUint32ToHex(CompiledDataSize);

		_writer.Write((byte)'\r');
		_writer.Write((byte)'\n');

		return true;
	}

	#endregion
}
