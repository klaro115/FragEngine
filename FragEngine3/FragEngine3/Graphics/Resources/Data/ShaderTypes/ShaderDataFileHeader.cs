using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

public struct ShaderDataFileHeader
{
	#region Fields

	// FORMAT IDENTIFIERS:

	public string formatSpecifier;  // 4 letter ASCII, file format specifier. Ex.: "FSHA" = 'Fragment Shader'
	public Version formatVersion;	// Version of the shader file format. Default: "10" (2x 4-bit number formatted as 2-digit hex)

	// CONTENT TABLE:

	public ushort fileHeaderSize;   // Total byte size of this file header. Default: "001C" (16-bit ushort formatted as 4-digit hex)
	public ushort jsonByteOffset;   // Byte offset from file start to the start of the JSON-encoded shader description. (16-bit ushort formatted as 4-digit hex)
	public ushort jsonByteLength;   // Byte size of the JSON-encoded shader description. Ex.: "CF31" (16-bit ushort formatted as 4-digit hex)
	public ushort sourceCodeOffset; // Byte offset of the source code header. Ex.: "CF95" (16-bit ushort formatted as 4-digit hex)
	public uint shaderDataOffset;	// Byte offset of the compiled shader blocks. Ex.: "0000F326" (32-bit ushort formatted as 8-digit hex)
	//...

	#endregion
	#region Constants

	public const int minFileHeaderSize = 5 + 3 + 4 * 5; // = 28 bytes

	#endregion
	#region Methods

	public static bool Read(BinaryReader _reader, out ShaderDataFileHeader _outFileHeader)
	{
		if (_reader is null)
		{
			_outFileHeader = default;
			return false;
		}

		// NOTE: The file header is encoded as an ASCII-string, with a known fixed character count.
		// Contents are meant to be both human- and machine-readable, without obscuring the shader
		// description that follows the header, when viewing the file in a normal text editor. All
		// contents of the shader description are JSON-encoded; all shader data thereafter is not.

		byte[] buffer = new byte[8];
		_outFileHeader = new();

		// Expected format: "FSHA_10_001C_001D_01B5_01D3_01D9\n"

		try
		{
			// Format identifiers:

			_reader.Read(buffer, 0, 4);
			_reader.ReadByte();
			_outFileHeader.formatSpecifier = $"{(char)buffer[0]}{(char)buffer[1]}{(char)buffer[2]}{(char)buffer[3]}";

			_reader.Read(buffer, 0, 2);
			_reader.ReadByte();
			_outFileHeader.formatVersion = new((int)HexCharToValue(buffer[0]), (int)HexCharToValue(buffer[1]));

			// Content table:

			_outFileHeader.fileHeaderSize = ReadUInt16(_reader, buffer);
			_outFileHeader.jsonByteOffset = ReadUInt16(_reader, buffer);
			_outFileHeader.jsonByteLength = ReadUInt16(_reader, buffer);
			_outFileHeader.sourceCodeOffset = ReadUInt16(_reader, buffer);
			_outFileHeader.shaderDataOffset = ReadUInt32(_reader, buffer);

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read file header of shader data file!", ex);
			_outFileHeader = default;
			return false;
		}
	}

	public readonly bool Write(BinaryWriter _writer)
	{
		if (_writer is null)
		{
			return false;
		}

		try
		{
			// Format identifiers:

			// File format specifier: (magic number)
			_writer.Write((byte)formatSpecifier[0]);	// 'F'
			_writer.Write((byte)formatSpecifier[1]);    // 'S'
			_writer.Write((byte)formatSpecifier[2]);    // 'H'
			_writer.Write((byte)formatSpecifier[3]);    // 'A'
			_writer.Write((byte)'_');

			_writer.Write(ValueToHexChar((uint)formatVersion.Major));	// 0-9, A-F
			_writer.Write(ValueToHexChar((uint)formatVersion.Minor));	// 0-9, A-F
			_writer.Write((byte)'_');

			// Content table:

			WriteUInt16(_writer, fileHeaderSize);
			WriteUInt16(_writer, jsonByteOffset);
			WriteUInt16(_writer, jsonByteLength);
			WriteUInt16(_writer, sourceCodeOffset);
			WriteUInt32(_writer, shaderDataOffset);

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to write file header of shader data file!", ex);
			return false;
		}
	}

	private static ushort ReadUInt16(BinaryReader _reader, byte[] _buffer)
	{
		_reader.Read(_buffer, 0, 4);	// 4x uppercase hex characters (0-9, A-F)
		_reader.ReadByte();				// trailing underscore ('_') or line break ('\n').
		uint value =
			(HexCharToValue(_buffer[0]) << 12) |
			(HexCharToValue(_buffer[1]) << 8) |
			(HexCharToValue(_buffer[2]) << 4) |
			(HexCharToValue(_buffer[3]) << 0);
		return (ushort)(value & 0xFFFF);
	}

	private static uint ReadUInt32(BinaryReader _reader, byte[] _buffer)
	{
		_reader.Read(_buffer, 0, 8);	// 4x uppercase hex characters (0-9, A-F)
		_reader.ReadByte();				// trailing underscore ('_') or line break ('\n').
		uint value =
			(HexCharToValue(_buffer[0]) << 28) |
			(HexCharToValue(_buffer[1]) << 24) |
			(HexCharToValue(_buffer[2]) << 20) |
			(HexCharToValue(_buffer[3]) << 16) |
			(HexCharToValue(_buffer[4]) << 12) |
			(HexCharToValue(_buffer[5]) << 8) |
			(HexCharToValue(_buffer[6]) << 4) |
			(HexCharToValue(_buffer[7]) << 0);
		return (ushort)(value & 0xFFFF);
	}

	private static void WriteUInt16(BinaryWriter _writer, ushort _value)
	{
		for (int i = 3; i >= 0; i--)
		{
			int shift = i * 4;
			byte hex = ValueToHexChar((uint)(_value >> shift));
			_writer.Write(hex);
		}
		_writer.Write((byte)'_');
	}

	private static void WriteUInt32(BinaryWriter _writer, uint _value)
	{
		for (int i = 7; i >= 0; i--)
		{
			int shift = i * 4;
			byte hex = ValueToHexChar(_value >> shift);
			_writer.Write(hex);
		}
		_writer.Write((byte)'_');
	}

	private static uint HexCharToValue(byte _x)
	{
		uint value = _x > '9'
			? _x - (uint)'A'
			: _x - (uint)'0';
		return value;
	}

	private static byte ValueToHexChar(uint value)
	{
		value &= 0x0Fu;
		uint hex = value > 9
			? value + '0'
			: value + 'A';
		return (byte)hex;
	}

	#endregion
}
