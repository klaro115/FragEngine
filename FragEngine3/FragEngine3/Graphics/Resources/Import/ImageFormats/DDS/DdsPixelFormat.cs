using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public sealed class DdsPixelFormat
{
	#region Fields

	public uint size;					// Byte size of the pixel format descriptor, must be 32 bytes.
	public DdsPixelFormatFlags flags;
	public uint fourCC;
	public uint rgbBitCount;
	public uint rBitMask;
	public uint gBitMask;
	public uint bBitMask;
	public uint aBitMask;

	#endregion
	#region Constants

	public const uint FOURCC_DXT10 = (byte)'D' | ((byte)'X' << 8) | ((byte)'1' << 16) | ((byte)'0' << 24);	//TODO: Needs testing. Packing ASCII with correct endian-ness is a nightmare I just don't want to deal with.

	#endregion
	#region Methods

	/// <summary>
	/// Performs a superficial check if the pixel format's data is at all plausible.
	/// </summary>
	/// <returns>True if the data is not utter nonsense, false otherwise.</returns>
	public bool IsValid()
	{
		bool result =
			size == 32 &&
			flags != 0 &&
			fourCC != 0;
		return result;
	}

	public static bool Read(BinaryReader _reader, out DdsPixelFormat _outFormat)
	{
		try
		{
			// Check leading size value:
			uint size = _reader.ReadUInt32();
			if (size != 32)
			{
				Logger.Instance?.LogError("DDS pixel format size mismatch; cannot read DDS pixel format!");
				_outFormat = null!;
				return false;
			}

			// Read data:
			_outFormat = new DdsPixelFormat()
			{
				size = size,
				flags = (DdsPixelFormatFlags)_reader.ReadUInt32(),
				fourCC = _reader.ReadUInt32(),
				rgbBitCount = _reader.ReadUInt32(),
				rBitMask = _reader.ReadUInt32(),
				gBitMask = _reader.ReadUInt32(),
				bBitMask = _reader.ReadUInt32(),
				aBitMask = _reader.ReadUInt32(),
			};

			return _outFormat.IsValid();
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read DDS pixel format!", ex);
			_outFormat = null!;
			return false;
		}
	}
	
	public bool Write(BinaryWriter _writer)
	{
		if (_writer is null)
		{
			Logger.Instance?.LogError("Cannot write DDS pixel format using null binary writer!");
			return false;
		}

		// Check if pixel format data is valid:
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot write DDS pixel format from invalid data!");
			return false;
		}

		try
		{
			// Write data:
			_writer.Write(size);
			_writer.Write((uint)flags);
			_writer.Write(fourCC);
			_writer.Write(rgbBitCount);
			_writer.Write(rBitMask);
			_writer.Write(gBitMask);
			_writer.Write(bBitMask);
			_writer.Write(aBitMask);

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to write DDS pixel format!", ex);
			return false;
		}
	}

	#endregion
}
