using System.Runtime.CompilerServices;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats;

public static class QoiImporter
{
	#region Types

	private struct QoiHeader
	{
		public uint magicNumbers;	// ASCII 'qoif'
		public uint width;			// Image width
		public uint height;			// Image height
		public byte channels;		// 3=RGB, 4=RGBA
		public bool isLinear;		// 0=sRGB with linear Alpha, 1=RGBA linear
	}

	#endregion
	#region Constants

	private const byte TAG_QOI_OP_RGB = 0xFE;
	private const byte TAG_QOI_OP_RGBA = 0xFF;
	private const byte TAG_QOI_OP_INDEX = 0x00;
	private const byte TAG_QOI_OP_DIFF = 0x40;
	private const byte TAG_QOI_OP_LUMA = 0x80;
	private const byte TAG_QOI_OP_RUN = 0xC0;

	#endregion
	#region Methods

	public static bool ImportImage(Stream _byteStream, out RawImageData? _outRawImage)				//TESTED
	{
		// Link with format reference: https://qoiformat.org/
		if (_byteStream == null)
		{
			Logger.Instance?.LogError("Cannot import QOI image from null stream!");
			_outRawImage = null;
			return false;
		}
		if (!_byteStream.CanRead)
		{
			Logger.Instance?.LogError("Cannot import QOI image from write-only stream!");
			_outRawImage = null;
			return false;
		}

		_outRawImage = new RawImageData()
		{
			channelCount = 4,
			bitsPerPixel = 32,
			isSrgb = false,

			pixelData_MonoByte = null,
			pixelData_MonoFloat = null,
			pixelData_RgbaByte = null,
			pixelData_RgbaFloat = null,
		};

		using BinaryReader reader = new(_byteStream);

		// Try reading file header:
		if (!ReadFileHeader(reader, out QoiHeader header))
		{
			return false;
		}

		// Initialize pixel buffer and settings:
		int pixelCount = (int)(header.width * header.height);

		_outRawImage.channelCount = header.channels;
		_outRawImage.isSrgb = !header.isLinear;
		_outRawImage.pixelData_RgbaByte = new RgbaByte[pixelCount];

		RgbaByte[] colorTable = new RgbaByte[64];
		Array.Fill(colorTable, new RgbaByte(0, 0, 0, 0));

		RgbaByte curColor = new(0, 0, 0, 255);
		int x;
		int pixelIndex = 0;
		while ((x = reader.ReadByte()) != -1 && pixelIndex < pixelCount)
		{
			if (x == TAG_QOI_OP_RGB)
			{
				// Set new RGB color, retain previous alpha:
				curColor = new(
					reader.ReadByte(),
					reader.ReadByte(),
					reader.ReadByte(),
					curColor.A);
				_outRawImage.pixelData_RgbaByte[pixelIndex++] = curColor;
				colorTable[GetColorIndexPosition(curColor)] = curColor;
			}
			else if (x == TAG_QOI_OP_RGBA)
			{
				// Set new RGBA color:
				curColor = new(
					reader.ReadByte(),
					reader.ReadByte(),
					reader.ReadByte(),
					reader.ReadByte());
				_outRawImage.pixelData_RgbaByte[pixelIndex++] = curColor;
				colorTable[GetColorIndexPosition(curColor)] = curColor;
			}
			else if ((x & 0xC0) == TAG_QOI_OP_INDEX)
			{
				// Load color from table:
				int colorIndex = x & 0x3F;
				curColor = colorTable[colorIndex];
				_outRawImage.pixelData_RgbaByte[pixelIndex++] = curColor;
				colorTable[GetColorIndexPosition(curColor)] = curColor;
			}
			else if ((x & 0xC0) == TAG_QOI_OP_DIFF)
			{
				// Apply minor difference to color:
				curColor = new(
					(byte)(curColor.R + ((x >> 4) & 0x3) - 2),
					(byte)(curColor.R + ((x >> 2) & 0x3) - 2),
					(byte)(curColor.R + ((x >> 0) & 0x3) - 2),
					curColor.A);
				_outRawImage.pixelData_RgbaByte[pixelIndex++] = curColor;
				colorTable[GetColorIndexPosition(curColor)] = curColor;
			}
			else if ((x & 0xC0) == TAG_QOI_OP_LUMA)
			{
				byte y = reader.ReadByte();

				// Read diffs to previous pixel:
				int dg = x & 0x3F;
				int drdg = (y >> 4) & 0x0F;
				int dbdg = (y >> 0) & 0x0F;

				// Apply signs to diff values:
				if ((dg & 0x20) == 0x20) dg = -(dg & 0x1F);
				if ((drdg & 0x8) == 0x8) drdg = -(drdg & 0x7);
				if ((dbdg & 0x8) == 0x8) dbdg = -(dbdg & 0x7);

				// Calculate new color values:
				byte g = (byte)(curColor.G + dg);
				byte r = (byte)(curColor.R + dg + drdg);
				byte b = (byte)(curColor.B + dg + dbdg);

				curColor = new(r, g, b, curColor.A);
				_outRawImage.pixelData_RgbaByte[pixelIndex++] = curColor;
				colorTable[GetColorIndexPosition(curColor)] = curColor;
			}
			else if ((x & 0xC0) == TAG_QOI_OP_RUN)
			{
				// Repeat current pixel:
				int runLength = x & 0x3F;
				Array.Fill(_outRawImage.pixelData_RgbaByte, curColor, pixelIndex, runLength);
				pixelIndex += runLength;
			}
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint BigToLittleEndian(uint _valueBE)
	{
		return
			((_valueBE >> 24) & 0xFFu) |
			((_valueBE >>  8) & 0xFFu) |
			((_valueBE <<  8) & 0xFFu) |
			((_valueBE >> 24) & 0xFFu);
	}
	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	//private static ushort BigToLittleEndian(ushort _valueBE)
	//{
	//	return (ushort)(((_valueBE >> 8) & 0xFFu) | ((_valueBE << 8) & 0xFFu));
	//}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetColorIndexPosition(RgbaByte _color)
	{
		return _color.R * 3 + _color.G * 5 + _color.B * 7 + _color.A * 11;
	}

	private static bool ReadFileHeader(BinaryReader _reader, out QoiHeader _outHeader)
	{
		try
		{
			_outHeader = new()
			{
				magicNumbers = _reader.ReadUInt32(),
				width = BigToLittleEndian(_reader.ReadUInt32()),
				height = BigToLittleEndian(_reader.ReadUInt32()),
				channels = _reader.ReadByte(),
				isLinear = _reader.ReadByte() == 1,
			};
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read QOI file header!", ex);
			_outHeader = default;
			return false;
		}
	}

	#endregion
}

