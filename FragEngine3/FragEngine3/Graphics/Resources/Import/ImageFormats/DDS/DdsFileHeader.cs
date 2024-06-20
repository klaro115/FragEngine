using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public sealed class DdsFileHeader
{
	#region Fields

	public uint magicNumber;			// ASCII magic number for DDS format: "DDS "
	
	public uint headerSize = 124;		// Header byte size (minus magic numbers)
	public DdsFileHeaderFlags flags;
	public uint height;
	public uint width;
	public uint pitchOrLinearSize;
	public uint depth;
	public uint mipMapCount;
	// [...] (reserved, 11x uint)
	public DdsPixelFormat pixelFormat = new();
	public uint caps;
	public uint caps2;
	public uint caps3;					//unused
	public uint caps4;					// unused
	// [...] (reserved, 1x uint)

	#endregion
	#region Constants

	public const uint MAGIC_NUMBER = 0x20534444u;	// Magic number encoding "DDS", padded with a whitespace.

	#endregion
	#region Properties

	/// <summary>
	/// Whether this file header is followed by a DXT10 header of type <see cref="DdsDxt10Header"/>. This is required for floating-point or non-linear surface formats.
	/// </summary>
	public bool HasDxt10Header => flags.HasFlag(DdsFileHeaderFlags.PixelFormat) && pixelFormat.fourCC == DdsPixelFormat.FOURCC_DXT10;

	#endregion
	#region Methods

	/// <summary>
	/// Performs a superficial check if the header's data is at all plausible.
	/// </summary>
	/// <returns>True if the data is not utter nonsense, false otherwise.</returns>
	public bool IsValid()
	{
		bool result =
			magicNumber == MAGIC_NUMBER &&
			headerSize == 124 &&
			width > 0;
		return result;
	}

	public static bool Read(BinaryReader _reader, out DdsFileHeader _outFileHeader)
	{
		if (_reader is null)
		{
			Logger.Instance?.LogError("Cannot read DDS file header using null binary reader!");
			_outFileHeader = null!;
			return false;
		}

		// Minimum valid file size is 128 bytes: (starting from current read position)
		long minRequiredLength = _reader.BaseStream.Position + 128;
		if (_reader.BaseStream.Length > 0 && _reader.BaseStream.Length < minRequiredLength)
		{
			Logger.Instance?.LogError("Stream is too short to contain valid data; cannot read DDS file header!");
			_outFileHeader = null!;
			return false;
		}

		try
		{
			//Check if magic numbers are correct:
			uint magicNumber = _reader.ReadUInt32();
			if (magicNumber != MAGIC_NUMBER)
			{
				Logger.Instance?.LogError("Incorrect magic numbers; cannot read DDS file header from invalid or unsupported data format!");
				_outFileHeader = null!;
				return false;
			}

			// Read magic numbers and first half of header:
			_outFileHeader = new()
			{
				magicNumber = magicNumber,
				headerSize = _reader.ReadUInt32(),
				flags = (DdsFileHeaderFlags)_reader.ReadUInt32(),
				height = _reader.ReadUInt32(),
				width = _reader.ReadUInt32(),
				pitchOrLinearSize = _reader.ReadUInt32(),
				depth = _reader.ReadUInt32(),
				mipMapCount = _reader.ReadUInt32(),
			};

			// Skip 44 bytes of reserved memory:
			for (uint i = 0; i < 11; ++i)
			{
				_reader.ReadUInt32();
			}

			// Read pixel format descriptor:
			if (!DdsPixelFormat.Read(_reader, out _outFileHeader.pixelFormat))
			{
				return false;
			}

			// Read remaining header:
			{
				_outFileHeader.caps = _reader.ReadUInt32();
				_outFileHeader.caps2 = _reader.ReadUInt32();
				_outFileHeader.caps3 = _reader.ReadUInt32();
				_outFileHeader.caps4 = _reader.ReadUInt32();
			}

			bool isFileHeaderValid = _outFileHeader.IsValid();
			return isFileHeaderValid;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read DDS image file format header!", ex);
			_outFileHeader = null!;
			return false;
		}
	}

	public bool Write(BinaryWriter _writer)
	{
		if (_writer is null)
		{
			Logger.Instance?.LogError("Cannot write DDS file header using null binary writer!");
			return false;
		}

		// Check if header data is valid:
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot write DDS file header from invalid data!");
			return false;
		}

		try
		{
			// Write magic numbers and first half of header:
			_writer.Write(magicNumber);
			_writer.Write(headerSize);
			_writer.Write((uint)flags);
			_writer.Write(height);
			_writer.Write(width);
			_writer.Write(pitchOrLinearSize);
			_writer.Write(depth);
			_writer.Write(mipMapCount);

			// Skip 44 bytes of reserved memory:
			const uint reserved = 0u;
			for (uint i = 0; i < 11; ++i)
			{
				_writer.Write(reserved);
			}

			// Read pixel format descriptor:
			if (!pixelFormat.Write(_writer))
			{
				return false;
			}

			// Write remaining header:
			{
				_writer.Write(caps);
				_writer.Write(caps2);
				_writer.Write(caps3);
				_writer.Write(caps4);
				_writer.Write(reserved);
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to write DDS file header!", ex);
			return false;
		}
	}

	#endregion
}
