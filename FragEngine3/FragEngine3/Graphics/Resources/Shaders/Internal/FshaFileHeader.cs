using FragEngine3.Graphics.Resources.Import;

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

		Version version = new(ReadHexUint8(_reader));

		ushort jsonOffset = ReadHexUint16(_reader);
		if (jsonOffset < MINIMUM_HEADER_SIZE)
		{
			_importCtx.Logger.LogError($"Invalid shader data header size! ({jsonOffset} bytes vs. {MINIMUM_HEADER_SIZE} bytes)");
			_outHeader = null!;
			return false;
		}
		ushort jsonSize = ReadHexUint16(_reader);

		ushort sourceCodeOffset = ReadHexUint16(_reader);
		ushort sourceCodeSize = ReadHexUint16(_reader);

		byte compiledDateBlockCount = ReadHexUint8(_reader);
		uint compiledDataOffset = ReadHexUint32(_reader);
		uint compiledDataSize = ReadHexUint32(_reader, false);

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

		WriteUint8ToHex(_writer, FileVersion.PackedVersion);

		WriteUint16ToHex(_writer, JsonOffset);
		WriteUint16ToHex(_writer, JsonSize);

		WriteUint16ToHex(_writer, SourceCodeOffset);
		WriteUint16ToHex(_writer, SourceCodeSize);

		WriteUint8ToHex(_writer, CompiledDataBlockCount);
		WriteUint32ToHex(_writer, CompiledDataOffset);
		WriteUint32ToHex(_writer, CompiledDataSize, false);

		_writer.Write((byte)'\r');
		_writer.Write((byte)'\n');

		return true;
	}

	private static byte ReadHexUint8(BinaryReader _reader)
	{
		uint c0 = ConvertHexToValue(_reader.ReadByte());
		uint c1 = ConvertHexToValue(_reader.ReadByte());
		_reader.ReadByte();
		return (byte)(c0 << 4 | c1);
	}
	private static ushort ReadHexUint16(BinaryReader _reader)
	{
		uint c0 = ConvertHexToValue(_reader.ReadByte());
		uint c1 = ConvertHexToValue(_reader.ReadByte());
		uint c2 = ConvertHexToValue(_reader.ReadByte());
		uint c3 = ConvertHexToValue(_reader.ReadByte());
		_reader.ReadByte();
		return (ushort)(c0 << 12 | c1 << 8 | c2 << 4 | c3);
	}
	private static uint ReadHexUint32(BinaryReader _reader, bool _skipTrailingUnderscore = true)
	{
		uint c0 = ConvertHexToValue(_reader.ReadByte());
		uint c1 = ConvertHexToValue(_reader.ReadByte());
		uint c2 = ConvertHexToValue(_reader.ReadByte());
		uint c3 = ConvertHexToValue(_reader.ReadByte());
		uint c4 = ConvertHexToValue(_reader.ReadByte());
		uint c5 = ConvertHexToValue(_reader.ReadByte());
		uint c6 = ConvertHexToValue(_reader.ReadByte());
		uint c7 = ConvertHexToValue(_reader.ReadByte());
		if (_skipTrailingUnderscore)
		{
			_reader.ReadByte();
		}
		return c0 << 28 | c1 << 24 | c2 << 20 | c3 << 16 | c4 << 12 | c5 << 8 | c6 << 4 | c7;
	}

	private static uint ConvertHexToValue(byte _hexChar)
	{
		return _hexChar >= 'A'
			? (uint)(_hexChar - 'A' + 10)
			: (uint)(_hexChar - '0');
	}

	private static void WriteUint8ToHex(BinaryWriter _writer, byte _value)
	{
		_writer.Write(ConvertNibbleToHex((_value & 0xF0u) >> 4));
		_writer.Write(ConvertNibbleToHex( _value & 0x0Fu));
		_writer.Write((byte)'_');
	}
	private static void WriteUint16ToHex(BinaryWriter _writer, ushort _value)
	{
		_writer.Write(ConvertNibbleToHex((_value & 0xF000u) >> 12));
		_writer.Write(ConvertNibbleToHex((_value & 0x0F00u) >> 8));
		_writer.Write(ConvertNibbleToHex((_value & 0x00F0u) >> 4));
		_writer.Write(ConvertNibbleToHex( _value & 0x000Fu));
		_writer.Write((byte)'_');
	}
	private static void WriteUint32ToHex(BinaryWriter _writer, uint _value, bool _addTrailingUnderscore = true)
	{
		_writer.Write(ConvertNibbleToHex((_value & 0xF0000000u) >> 28));
		_writer.Write(ConvertNibbleToHex((_value & 0x0F000000u) >> 24));
		_writer.Write(ConvertNibbleToHex((_value & 0x00F00000u) >> 20));
		_writer.Write(ConvertNibbleToHex((_value & 0x000F0000u) >> 16));
		_writer.Write(ConvertNibbleToHex((_value & 0x0000F000u) >> 12));
		_writer.Write(ConvertNibbleToHex((_value & 0x00000F00u) >> 8));
		_writer.Write(ConvertNibbleToHex((_value & 0x000000F0u) >> 4));
		_writer.Write(ConvertNibbleToHex( _value & 0x0000000Fu));
		if (_addTrailingUnderscore)
		{
			_writer.Write((byte)'_');
		}
	}

	private static byte ConvertNibbleToHex(uint _uint4)
	{
		return _uint4 >= 10
			? (byte)(_uint4 + 'A' - 10)
			: (byte)(_uint4 + '0');
	}

	#endregion
}
