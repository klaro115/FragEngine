using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import
{
	public static class BitmapImporter
	{
		#region Types

		private struct BmpHeader
		{
			public ushort bfType;
			public uint bfSize;
			public uint bfReserved;
			public uint bfOffBits;
		}

		private struct BmpInfoBlock
		{
			public uint bfSize;
			public int biWidth;
			public int biHeight;
			public ushort biPlanes;
			public ushort biBitCount;
			public uint biCompression;
			public uint biSizeImage;
			public int biXPelsPerMeter;
			public int biYPelsPerMeter;
			public uint biClrUsed;
			public uint biClrImportant;
		}

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

			_outRawImage = new RawImageData();

			long streamStartPosition = _byteStream.Position;
			BinaryReader reader = new(_byteStream);

			if (!ReadHeader(reader, out BmpHeader header))
			{
				Logger.Instance?.LogError("Failed to parse header of bitmap image!");
				return false;
			}

			if (!ReadInfoBlock(reader, out BmpInfoBlock infoBlock))
			{
				Logger.Instance?.LogError("Failed to parse info block of bitmap image!");
				return false;
			}
			
			if (!ReadColorMasks(reader))	//TODO: Conditional!
			{
				Logger.Instance?.LogError("Failed to parse color masks of bitmap image!");
				return false;
			}

			if (!ReadColorTable(reader))	//TODO: Conditional!
			{
				Logger.Instance?.LogError("Failed to parse color table of bitmap image!");
				return false;
			}

			// Move to pixel data block:
			if (_byteStream.CanSeek)
			{
				_byteStream.Position = streamStartPosition + header.bfOffBits;
			}

			//TODO: Parse pixel data.

			return true;
		}

		private static bool ReadHeader(BinaryReader _reader, out BmpHeader _outHeader)
		{
			try
			{
				_outHeader = new BmpHeader()
				{
					bfType			= _reader.ReadUInt16(),	// ASCII "BM"
					bfSize			= _reader.ReadUInt32(),	// File size in bytes
					bfReserved		= _reader.ReadUInt32(),	// Reserved, should be 0
					bfOffBits		= _reader.ReadUInt32(),	// Byte offset where image data starts, usually 54
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
					bfSize			= _reader.ReadUInt32(),
					biWidth			= _reader.ReadInt32(),
					biHeight		= _reader.ReadInt32(),
					biPlanes		= _reader.ReadUInt16(),
					biBitCount		= _reader.ReadUInt16(),
					biCompression	= _reader.ReadUInt32(),
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

		private static bool ReadColorMasks(BinaryReader _reader /* TODO */)
		{
			//TODO
			return false;
		}

		private static bool ReadColorTable(BinaryReader _reader /* TODO */)
		{
			//TODO
			return false;
		}

		#endregion
	}
}
