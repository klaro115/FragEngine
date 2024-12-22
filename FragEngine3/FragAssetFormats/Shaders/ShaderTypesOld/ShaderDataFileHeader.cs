using FragEngine3.EngineCore;

namespace FragAssetFormats.Shaders.ShaderTypes;

public struct ShaderDataFileHeader()
{
	#region Fields

	// FORMAT IDENTIFIERS:

	public string formatSpecifier = ShaderDataConstants.FHSA_FORMAT_SPECIFIER;  // 4 letter ASCII, file format specifier. Ex.: "FSHA" = 'Fragment Shader'
	public ShaderDataVersion formatVersion = new();     // Version of the shader file format. Default: "10" (2x 4-bit number formatted as 2-digit hex)

	// CONTENT TABLE:

	public ushort fileHeaderSize = minFileHeaderSize;   // Total byte size of this file header. Default: "001C" (16-bit format)
	public ShaderDataOffsetAndSize jsonDescription;     // JSON-encoded shader description. (16-bit format)
	public ShaderDataOffsetAndSize sourceCode;          // Optional shader source code, generally in HLSL, encoded as UTF-8 or ASCII. (16-bit format)
	public byte shaderDataBlockCount;                   // Number of shader variant blocks in shader data. (16-bit format)
	public ShaderDataOffsetAndSize shaderData;          // Shader data, arranged as contiguous blocks of compiled variants. (32-bit format)
														//...

	#endregion
	#region Constants

	public const int minFileHeaderSize = 55;    // 0x37

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

		// Expected format: "FSHA_10_0039_0043_065E_06AB_7CBB_04_00008370_00003F20\n"

		try
		{
			// Format identifiers:

			_reader.Read(buffer, 0, 4);
			_reader.ReadByte();
			_outFileHeader.formatSpecifier = $"{(char)buffer[0]}{(char)buffer[1]}{(char)buffer[2]}{(char)buffer[3]}";

			if (_outFileHeader.formatSpecifier != "FSHA")
			{
				Logger.Instance?.LogError($"Shader asset file uses invalid format specifier '{_outFileHeader.formatSpecifier}' where 'FSHA' was expected! Aborting import.");
				return false;
			}

			_reader.Read(buffer, 0, 2);
			_reader.ReadByte();
			_outFileHeader.formatVersion = new()
			{
				major = (byte)ShaderDataReadWriteHelper.HexCharToValue(buffer[0]),
				minor = (byte)ShaderDataReadWriteHelper.HexCharToValue(buffer[1])
			};

			// Content table:

			_outFileHeader.fileHeaderSize = ShaderDataReadWriteHelper.ReadUInt16(_reader, buffer);
			_outFileHeader.jsonDescription = ShaderDataOffsetAndSize.Read16(_reader, buffer);
			_outFileHeader.sourceCode = ShaderDataOffsetAndSize.Read16(_reader, buffer);
			_outFileHeader.shaderDataBlockCount = ShaderDataReadWriteHelper.ReadUInt8(_reader, buffer);
			_outFileHeader.shaderData = ShaderDataOffsetAndSize.Read32(_reader, buffer);

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
			_writer.Write((byte)formatSpecifier[0]);    // 'F'
			_writer.Write((byte)formatSpecifier[1]);    // 'S'
			_writer.Write((byte)formatSpecifier[2]);    // 'H'
			_writer.Write((byte)formatSpecifier[3]);    // 'A'
			_writer.Write((byte)'_');

			_writer.Write(ShaderDataReadWriteHelper.ValueToHexChar(formatVersion.major));   // 0-9, A-F
			_writer.Write(ShaderDataReadWriteHelper.ValueToHexChar(formatVersion.minor));   // 0-9, A-F
			_writer.Write((byte)'_');

			// Content table:

			ShaderDataReadWriteHelper.WriteUInt16(_writer, fileHeaderSize);
			_writer.Write((byte)'_');
			jsonDescription.Write16(_writer);
			_writer.Write((byte)'_');
			sourceCode.Write16(_writer);
			_writer.Write((byte)'_');
			ShaderDataReadWriteHelper.WriteUInt8(_writer, shaderDataBlockCount);
			_writer.Write((byte)'_');
			shaderData.Write32(_writer);
			_writer.Write((byte)'\r');
			_writer.Write((byte)'\n');

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to write file header of shader data file!", ex);
			return false;
		}
	}

	#endregion
}
