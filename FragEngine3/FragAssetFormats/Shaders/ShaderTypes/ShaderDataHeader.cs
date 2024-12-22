using FragAssetFormats.Contexts;

namespace FragAssetFormats.Shaders.ShaderTypes;

[Serializable]
public sealed class ShaderDataHeader
{
	#region Types

	[Serializable]
	public readonly struct Version(byte _packedVersion)
	{
		public readonly byte major = (byte)((_packedVersion & 0xF0u) >> 4);
		public readonly byte minor = (byte)(_packedVersion & 0x0Fu);

		public byte PackedVersion => (byte)((major << 4) | minor);

		public static Version Current => new(CURRENT_VERSION);
	}

	#endregion
	#region Properties

	// FILE DATA:

	public required uint HeaderSize { get; init; } = MINIMUM_HEADER_SIZE;
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

	public const uint MINIMUM_HEADER_SIZE = 55;     // 0x37

	public const uint MAGIC_NUMBERS = ((uint)'F' << 0) | ((uint)'S' << 8) | ((uint)'H' << 16) | ((uint)'A' << 24);

	public const byte CURRENT_VERSION = (0x00 << 4) | (0x02);

	#endregion
	#region Methods

	public static bool Read(in ImporterContext _importCtx, BinaryReader _reader, out ShaderDataHeader _outHeader)
	{
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data");
			_outHeader = null!;
			return false;
		}

		uint magicNumbers = _reader.ReadUInt32();
		if (magicNumbers != MAGIC_NUMBERS)
		{
			_importCtx.Logger.LogError($"Magic numbers of file header indicate unsupported file format! ({magicNumbers:H} vs. ({MAGIC_NUMBERS:H}))");
			_outHeader = null!;
			return false;
		}

		Version version = new(_reader.ReadByte());

		ushort headerSize = _reader.ReadUInt16();
		if (headerSize < MINIMUM_HEADER_SIZE)
		{
			_importCtx.Logger.LogError($"Invalid shader data header size! ({headerSize} bytes vs. {MINIMUM_HEADER_SIZE} bytes)");
			_outHeader = null!;
			return false;
		}

		ushort jsonOffset = _reader.ReadUInt16();
		ushort jsonSize = _reader.ReadUInt16();

		ushort sourceCodeOffset = _reader.ReadUInt16();
		ushort sourceCodeSize = _reader.ReadUInt16();

		byte compiledDateBlockCount = _reader.ReadByte();
		uint compiledDataOffset = _reader.ReadUInt32();
		uint compiledDataSize = _reader.ReadUInt32();

		_outHeader = new ShaderDataHeader()
		{
			FileVersion = version,
			HeaderSize = headerSize,

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

	public bool Write(in ImporterContext _importCtx, BinaryWriter _writer)
	{
		if (_writer is null)
		{
			_importCtx.Logger.LogError("Cannot write shader data header to null binary writer!");
			return false;
		}

		_writer.Write(MAGIC_NUMBERS);
		_writer.Write(FileVersion.PackedVersion);

		_writer.Write(JsonOffset);
		_writer.Write(JsonSize);

		_writer.Write(SourceCodeOffset);
		_writer.Write(SourceCodeSize);

		_writer.Write(CompiledDataBlockCount);
		_writer.Write(CompiledDataOffset);
		_writer.Write(CompiledDataSize);

		return true;
	}

	#endregion
}
