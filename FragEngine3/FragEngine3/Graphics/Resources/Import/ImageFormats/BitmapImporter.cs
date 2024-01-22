using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats
{
	public static class BitmapImporter
	{
		#region Types

		private enum BiCompression : uint
		{
			BI_RGB = 0,							// uncompressed
			BI_RLE8 = 1,						// running length encoding, requires: biBitCount=8, biHeight>0
			BI_RLE4 = 2,						// running length encoding, requires: biBitCount=4, biHeight>0
			BI_BITFIELDS = 3,					// uncompressed, but using bit masks, requires: biBitCount=16 or 32.
		}

		private struct BmpHeader
		{
			public ushort bfType;               // ASCII "BM"
			public uint bfSize;                 // File byte size (unreliable).
			public uint bfReserved;             // unused, should be 0.
			public uint bfOffBits;              // Byte offset from file start to image data start.
		}

		private struct BmpInfoBlock
		{
			public uint biSize;                 // Info block byte size.
			public int biWidth;
			public int biHeight;                // if negative, image is top-down, if positive, image is bottom-up.
			public ushort biPlanes;             // unused, should be 1.
			public ushort biBitCount;           // Bits per pixel, must be 1, 4, 8, 16, 24, or 32.
			public BiCompression biCompression;
			public uint biSizeImage;            // Byte size of image data, May be 0 if uncompressed RGB. 
			public int biXPelsPerMeter;         // usually 0.
			public int biYPelsPerMeter;         // usually 0.
			public uint biClrUsed;              // Number of colors in color table. If biBitCount is 1, 4 or 8 => 0 means max count (2, 16, or 256). If other, number is accurate.
			public uint biClrImportant;

			public readonly bool ColorsAreIndexed => biBitCount <= 8;
			public readonly bool HasColorMasks => biCompression == BiCompression.BI_BITFIELDS;
			public readonly bool HasColorTables => biBitCount <= 8 || biClrUsed != 0;
			public readonly bool UseRleCompression => biCompression == BiCompression.BI_RLE4 || biCompression == BiCompression.BI_RLE8;
		}

		private struct BmpColorMasks
		{
			public uint maskR;
			public uint maskG;
			public uint maskB;
		}
		private struct BmpColorTable
		{
			public RgbaByte[] colors;
		}

		#endregion
		#region Constants

		private const float PPM2DPI = 0.0254f;

		#endregion
		#region Methods

		public static bool ImportImage(Stream _byteStream, out RawImageData? _outRawImage)
		{
			// Link with format reference: https://de.wikipedia.org/wiki/Windows_Bitmap
			if (_byteStream == null)
			{
				Logger.Instance?.LogError("Cannot import bitmap image from null stream!");
				_outRawImage = null;
				return false;
			}
			if (!_byteStream.CanRead)
			{
				Logger.Instance?.LogError("Cannot import bitmap image from write-only stream!");
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

			long streamStartPosition = _byteStream.Position;
			using BinaryReader reader = new(_byteStream);

			// Read file header:
			if (!ReadHeader(reader, out BmpHeader header))
			{
				Logger.Instance?.LogError("Failed to parse header of bitmap image!");
				return false;
			}

			// Read image description header:
			if (!ReadInfoBlock(reader, out BmpInfoBlock infoBlock))
			{
				Logger.Instance?.LogError("Failed to parse info block of bitmap image!");
				return false;
			}

			// [Optional] Read color masks:
			BmpColorMasks colorMasks = default;
			if (infoBlock.HasColorMasks && !ReadColorMasks(reader, out colorMasks))
			{
				Logger.Instance?.LogError("Failed to parse color masks of bitmap image!");
				return false;
			}

			// [Optional] Read color tables:
			BmpColorTable colorTable = default;
			if (infoBlock.HasColorTables && !ReadColorTable(reader, in infoBlock, out colorTable))
			{
				Logger.Instance?.LogError("Failed to parse color table of bitmap image!");
				return false;
			}

			// Jump to pixel data block:
			if (_byteStream.CanSeek)
			{
				_byteStream.Position = streamStartPosition + header.bfOffBits;
			}
			else
			{
				for (uint i = (uint)_byteStream.Position; i < header.bfOffBits; i++)
				{
					_byteStream.ReadByte();
				}
			}

			// Update values on image data object:
			_outRawImage.width = (uint)infoBlock.biWidth;
			_outRawImage.height = (uint)Math.Abs(infoBlock.biHeight);
			_outRawImage.dpi = infoBlock.biXPelsPerMeter != 0 ? (uint)(infoBlock.biXPelsPerMeter * PPM2DPI) : 72;

			// Read pixel data:
			if (!ReadPixelData(reader, in infoBlock, in colorMasks, in colorTable, _outRawImage))
			{
				Logger.Instance?.LogError("Failed to parse info block of bitmap image!");
				return false;
			}

			return true;
		}

		private static bool ReadHeader(BinaryReader _reader, out BmpHeader _outHeader)
		{
			try
			{
				_outHeader = new BmpHeader()
				{
					bfType			= _reader.ReadUInt16(),  // ASCII "BM"
					bfSize			= _reader.ReadUInt32(),  // File size in bytes
					bfReserved		= _reader.ReadUInt32(),  // Reserved, should be 0
					bfOffBits		= _reader.ReadUInt32(),   // Byte offset where image data starts, usually 54
				};
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException("Failed to read bitmap image header from byte stream!", ex);
				_outHeader = default;
				return false;
			}
		}

		private static bool ReadInfoBlock(BinaryReader _reader, out BmpInfoBlock _outInfoBlock)
		{
			try
			{
				_outInfoBlock = new BmpInfoBlock()
				{
					biSize			= _reader.ReadUInt32(),
					biWidth			= _reader.ReadInt32(),
					biHeight		= _reader.ReadInt32(),
					biPlanes		= _reader.ReadUInt16(),
					biBitCount		= _reader.ReadUInt16(),
					biCompression	= (BiCompression)_reader.ReadUInt32(),
					biSizeImage		= _reader.ReadUInt32(),
					biXPelsPerMeter	= _reader.ReadInt32(),
					biYPelsPerMeter	= _reader.ReadInt32(),
					biClrUsed		= _reader.ReadUInt16(),
					biClrImportant	= _reader.ReadUInt16(),
				};
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException("Failed to read bitmap image info block from byte stream!", ex);
				_outInfoBlock = default;
				return false;
			}
		}

		private static bool ReadColorMasks(BinaryReader _reader, out BmpColorMasks _outColorMasks)
		{
			try
			{
				_outColorMasks = new BmpColorMasks()
				{
					maskR = _reader.ReadUInt32(),
					maskG = _reader.ReadUInt32(),
					maskB = _reader.ReadUInt32(),
				};
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException("Failed to read bitmap color masks from byte stream!", ex);
				_outColorMasks = default;
				return false;
			}
		}

		private static bool ReadColorTable(BinaryReader _reader, in BmpInfoBlock _infoBlock, out BmpColorTable _outColorTable)
		{
			// Determine how many entries are in the color table:
			uint tableSize;
			if (_infoBlock.biClrUsed == 0)
			{
				tableSize = _infoBlock.biBitCount switch
				{
					1 => 2,
					4 => 16,
					8 => 256,
					_ => 0,
				};
			}
			else
			{
				tableSize = _infoBlock.biClrUsed;
			}

			if (tableSize == 0)
			{
				Logger.Instance?.LogError("Cannot determine bitmap color table size!");
				_outColorTable = default;
				return false;
			}

			// Create the table:
			_outColorTable = new BmpColorTable()
			{
				colors = new RgbaByte[tableSize],
			};

			// Read color data:
			try
			{
				for (uint i = 0; i < tableSize; ++i)
				{
					byte b = _reader.ReadByte();
					byte g = _reader.ReadByte();
					byte r = _reader.ReadByte();
					_reader.ReadByte(); //padding, must be 0.

					_outColorTable.colors[i] = new RgbaByte(r, g, b, 0xFF);
				}
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException("Failed to read bitmap color table from byte stream!", ex);
				_outColorTable = default;
				return false;
			}
		}

		private static bool ReadPixelData(BinaryReader _reader, in BmpInfoBlock _infoBlock, in BmpColorMasks _colorMasks, in BmpColorTable _colorTable, RawImageData _rawImage)
		{
			_rawImage.pixelData_RgbaByte = new RgbaByte[_rawImage.PixelCount];

			try
			{
				switch (_infoBlock.biBitCount)
				{
					case 1:
						return ReadPixelData_Indexed_1(_reader, in _infoBlock, in _colorTable, _rawImage.pixelData_RgbaByte);

					case 4:
						if (_infoBlock.biCompression == BiCompression.BI_RLE4)
						{
							//TODO [whenever]: 4-bit RLE compressed color data.
							Logger.Instance?.LogError("4-bit run-length encoding (RLE) for bitmap is not supported at this time!");
							return false;
						}
						else
						{
							return ReadPixelData_Indexed_4(_reader, in _infoBlock, in _colorTable, _rawImage.pixelData_RgbaByte);
						}

					case 8:
						if (_infoBlock.biCompression == BiCompression.BI_RLE8)
						{
							return ReadPixelData_RLE_8(_reader, in _infoBlock, _rawImage.pixelData_RgbaByte);
						}
						else
						{
							return ReadPixelData_Indexed_8(_reader, in _infoBlock, in _colorTable, _rawImage.pixelData_RgbaByte);
						}

					case 16:
						if (_infoBlock.biCompression == BiCompression.BI_BITFIELDS)
						{
							//TODO: Uncompressed 16-bit color using color masks.
							return false;
						}
						else
						{
							return ReadPixelData_Uncompressed_16(_reader, in _infoBlock, _rawImage.pixelData_RgbaByte);
						}

					case 24:
						return ReadPixelData_Uncompressed_24(_reader, in _infoBlock, _rawImage.pixelData_RgbaByte);

					case 32:
						if (_infoBlock.biCompression == BiCompression.BI_BITFIELDS)
						{
							//TODO: Uncompressed 32-bit color using color masks.
							return false;
						}
						else
						{
							return ReadPixelData_Uncompressed_32(_reader, in _infoBlock, _rawImage.pixelData_RgbaByte);
						}

					default:
						{
							Logger.Instance?.LogError($"Bitmap image file uses invalid bit depth! (biBitCount={_infoBlock.biBitCount})!");
							return false;
						}
				}
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to read bitmap image pixel data from byte stream! (biBitCount={_infoBlock.biBitCount})", ex);
				return false;
			}
		}

		private static bool ReadPixelData_Indexed_1(BinaryReader _reader, in BmpInfoBlock _infoBlock, in BmpColorTable _colorTable, RgbaByte[] _pixelData)
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int srcStartY;
			int srcDirY;
			if (_infoBlock.biHeight < 0)
			{
				srcStartY = height;
				srcDirY = -1;
			}
			else
			{
				srcStartY = 0;
				srcDirY = 1;
			}

			RgbaByte color0 = _colorTable.colors[0];
			RgbaByte color1 = _colorTable.colors[1];
			if (color0 == color1 && color0 == RgbaByte.Black)
			{
				color1 = RgbaByte.White;
			}

			int eighthWidth = width / 8;
			for (int y = 0; y < height; y++)
			{
				int dstRowIdx = srcStartY + y * srcDirY;
				int dstRowStartIdx = dstRowIdx * width;
				for (int x = 0; x < eighthWidth; ++x)
				{
					byte eightIdx = _reader.ReadByte();
					int dstIdxStart = dstRowStartIdx + 8 * x;

					for (int i = 0; i < 8; ++i)
					{
						int srcIdx = eightIdx >> 7 - i & 1;
						_pixelData[dstIdxStart + i] = srcIdx == 1
							? color1
							: color0;
					}
				}
			}

			return true;
		}

		private static bool ReadPixelData_Indexed_4(BinaryReader _reader, in BmpInfoBlock _infoBlock, in BmpColorTable _colorTable, RgbaByte[] _pixelData)      //looks ugly
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int srcStartY;
			int srcDirY;
			if (_infoBlock.biHeight < 0)
			{
				srcStartY = height;
				srcDirY = -1;
			}
			else
			{
				srcStartY = 0;
				srcDirY = 1;
			}

			int halfWidth = width / 2;
			for (int y = 0; y < height; y++)
			{
				int dstRowIdx = srcStartY + y * srcDirY;
				int dstRowStartIdx = dstRowIdx * width;
				for (int x = 0; x < halfWidth; ++x)
				{
					byte doubleIdx = _reader.ReadByte();
					int srcIdx0 = (doubleIdx >> 4) & 0x0F;
					int srcIdx1 = (doubleIdx >> 0) & 0x0F;
					int dstIdxStart = dstRowStartIdx + 2 * x;

					_pixelData[dstIdxStart + 0] = _colorTable.colors[srcIdx0];
					_pixelData[dstIdxStart + 1] = _colorTable.colors[srcIdx1];
				}
			}

			return true;
		}

		private static bool ReadPixelData_Indexed_8(BinaryReader _reader, in BmpInfoBlock _infoBlock, in BmpColorTable _colorTable, RgbaByte[] _pixelData)      //looks ugly
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int srcStartY;
			int srcDirY;
			if (_infoBlock.biHeight < 0)
			{
				srcStartY = height;
				srcDirY = -1;
			}
			else
			{
				srcStartY = 0;
				srcDirY = 1;
			}

			for (int y = 0; y < height; y++)
			{
				int dstRowIdx = srcStartY + y * srcDirY;
				int dstRowStartIdx = dstRowIdx * width;
				for (int x = 0; x < width; ++x)
				{
					byte srcIdx = _reader.ReadByte();

					_pixelData[dstRowStartIdx + x] = _colorTable.colors[srcIdx];
				}
			}

			return true;
		}

		private static bool ReadPixelData_Uncompressed_16(BinaryReader _reader, in BmpInfoBlock _infoBlock, RgbaByte[] _pixelData)      //TODO: Untested
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int srcStartY;
			int srcDirY;
			if (_infoBlock.biHeight < 0)
			{
				srcStartY = height;
				srcDirY = -1;
			}
			else
			{
				srcStartY = 0;
				srcDirY = 1;
			}

			for (int y = 0; y < height; y++)
			{
				int dstRowIdx = srcStartY + y * srcDirY;
				int dstRowStartIdx = dstRowIdx * width;
				for (int x = 0; x < width; ++x)
				{
					ushort packed = _reader.ReadUInt16();   // RGB555, 1st bit is unused
					byte r = (byte)((packed >> 10) & 0x1F);
					byte g = (byte)((packed >>  5) & 0x1F);
					byte b = (byte)((packed >>  0) & 0x1F);

					_pixelData[dstRowStartIdx + x] = new(r, g, b, 0xFF);
				}
			}

			return true;
		}

		private static bool ReadPixelData_Uncompressed_24(BinaryReader _reader, in BmpInfoBlock _infoBlock, RgbaByte[] _pixelData)
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int srcStartY;
			int srcDirY;
			if (_infoBlock.biHeight < 0)
			{
				srcStartY = height;
				srcDirY = -1;
			}
			else
			{
				srcStartY = 0;
				srcDirY = 1;
			}

			for (int y = 0; y < height; y++)
			{
				int dstRowIdx = srcStartY + y * srcDirY;
				int dstRowStartIdx = dstRowIdx * width;
				for (int x = 0; x < width; ++x)
				{
					byte b = _reader.ReadByte();
					byte g = _reader.ReadByte();
					byte r = _reader.ReadByte();

					_pixelData[dstRowStartIdx + x] = new(r, g, b, 0xFF);
				}
			}

			return true;
		}

		private static bool ReadPixelData_Uncompressed_32(BinaryReader _reader, in BmpInfoBlock _infoBlock, RgbaByte[] _pixelData)      //TODO: Untested
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int srcStartY;
			int srcDirY;
			if (_infoBlock.biHeight < 0)
			{
				srcStartY = height;
				srcDirY = -1;
			}
			else
			{
				srcStartY = 0;
				srcDirY = 1;
			}

			for (int y = 0; y < height; y++)
			{
				int dstRowIdx = srcStartY + y * srcDirY;
				int dstRowStartIdx = dstRowIdx * width;
				for (int x = 0; x < width; ++x)
				{
					uint packed = _reader.ReadUInt32();
					byte a = (byte)((packed >> 24) & 0xFF);
					byte r = (byte)((packed >> 16) & 0xFF);
					byte g = (byte)((packed >>  8) & 0xFF);
					byte b = (byte)((packed >>  0) & 0xFF);

					_pixelData[dstRowStartIdx + x] = new(r, g, b, a);
				}
			}

			return true;
		}

		private static bool ReadPixelData_RLE_8(BinaryReader _reader, in BmpInfoBlock _infoBlock, RgbaByte[] _pixelData)        //TODO: Untested
		{
			int height = Math.Abs(_infoBlock.biHeight);
			int width = _infoBlock.biWidth;
			int pixelCount = width * height;

			int dstStartY;
			int dstDirY;
			if (_infoBlock.biHeight < 0)
			{
				dstStartY = height;
				dstDirY = -1;
			}
			else
			{
				dstStartY = 0;
				dstDirY = 1;
			}

			// Prepare byte array for intermediate uncompressed buffering:
			const int decodedPixelSize = 4;
			byte[] byteBuffer = new byte[pixelCount * decodedPixelSize];

			int x = 0;
			int y = dstStartY;

			int pixelsRead = 0;
			int leadByte;
			while (pixelsRead < pixelCount && (leadByte = _reader.Read()) >= 0)
			{
				byte valueByte = _reader.ReadByte();

				// If leading byte is 0, treat subsequent data as an instruction:
				if (leadByte == 0)
				{
					// New line:
					if (valueByte == 0)
					{
						x = 0;
						y += dstDirY;
					}
					// End of data:
					else if (valueByte == 1)
					{
						break;
					}
					// Jump to new position:
					else if (valueByte == 2)
					{
						byte jumpOffsetX = _reader.ReadByte();
						byte jumpOffsetY = _reader.ReadByte();
						x += jumpOffsetX;
						y += dstDirY * jumpOffsetY;
					}
				}
				// If first byte has non-zero value N, repeat the subsequent byte N times:
				else
				{
					int dstStartIdx = y * width + x;
					for (int i = 0; i < leadByte; ++i)
					{
						int dstIdx = dstStartIdx + i;
						byteBuffer[dstIdx] = valueByte;
						x++;
					}
				}
			}

			// Convert buffered data to colors we can use:
			for (int i = 0; i < pixelCount; ++i)
			{
				int bufferStartIdx = decodedPixelSize * i;
				byte b = byteBuffer[bufferStartIdx + 0];
				byte g = byteBuffer[bufferStartIdx + 1];
				byte r = byteBuffer[bufferStartIdx + 2];
				byte a = byteBuffer[bufferStartIdx + 3];    //TEMP: Not sure if this decodes to 32-bit or 24-bit, or whatever BPP is written in '_infoBlock.biBitCount'.

				_pixelData[i] = new RgbaByte(r, g, b, a);
			}

			return true;
		}

		#endregion
	}
}
