using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Resources;
using ImageMagick;
using Veldrid;

namespace FragAssetFormats.Images;

/// <summary>
/// Importer for the various image file formats.<para/>
/// NOTE: This is used as a backup for any image formats not implemented directly be the engine.
/// </summary>
public sealed class MagickImporter : IImageImporter
{
	#region Types

	[Flags]
	public enum ImportFlags
	{
		None	= 0,

		SDR		= 1,
		HDR		= 2,
		//...
	}

	#endregion
	#region Fields

	private static readonly string[] supportedFormatExtensions =
	[
		".bmp",
		".exr",
		".gif",
		".jpeg",
		".jpg",
		".png",
		".tga",
		".tif",
		".tiff",
		".webp",
		//...
	];

	#endregion
	#region Constants

	private const float PPCm2DPI = 2.54f;

	#endregion
	#region Properties

	public uint MinimumBitDepth => 1;
	public uint MaximumBitDepth => 16;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFormatExtensions;

	/// <summary>
	/// Checks whether a given file format is at all supported.
	/// </summary>
	/// <param name="_fileExtension">The file extension of the image format.</param>
	/// <returns>True if the format is supported, false otherwise. This will also return true in case of partial support.</returns>
	public static bool SupportsFormat(string _fileExtension)
	{
		if (string.IsNullOrEmpty(_fileExtension))
		{
			return false;
		}

		return supportedFormatExtensions.Any(o => string.Compare(o, _fileExtension, StringComparison.InvariantCultureIgnoreCase) == 0);
	}

	/// <summary>
	/// Find and parse any additional processinng flags that need to be respected during import.
	/// </summary>
	/// <param name="_importFlags">A string containing the resource's import flags.</param>
	/// <returns>Bit flags for all import flags that were detected.</returns>
	public static ImportFlags ParseImportFlags(string? _importFlags)
	{
		if (string.IsNullOrEmpty(_importFlags)) return 0;

		ImportFlags flags = 0;

		// In SDR, only use 8 bits per channel, using byte type:
		if (_importFlags.Contains("SDR", StringComparison.OrdinalIgnoreCase))
		{
			flags |= ImportFlags.SDR;
		}
		// In HDR, allow 32 bits per channel, using float type:
		else if (_importFlags.Contains("HDR", StringComparison.OrdinalIgnoreCase))
		{
			flags |= ImportFlags.HDR;
		}

		//...

		return flags;
	}

	public bool ImportImageData(in ImporterContext _importCtx, Stream _stream, out RawImageData _outRawImage)
	{
		ImportFlags hdrFlags = _importCtx.PreferHDR ? ImportFlags.HDR : ImportFlags.SDR;

		bool success = ImportImage(_stream, hdrFlags, out _outRawImage);
		return success;
	}

	public static bool ImportImage(Stream _stream, ImportFlags _importFlags, out RawImageData _outRawImage)
	{
		if (_stream == null)
		{
			Logger.Instance?.LogError("Cannot import image file from null stream!");
			_outRawImage = null!;
			return false;
		}
		if (!_stream.CanRead)
		{
			Logger.Instance?.LogError("Cannot import image file from write-only stream!");
			_outRawImage = null!;
			return false;
		}

		_outRawImage = new RawImageData();

		try
		{
			using MagickImage img = new(_stream);
			// Ensure the image's color space is something we can work with:
			switch (img.ColorSpace)
			{
				case ColorSpace.RGB:
				case ColorSpace.sRGB:
					break;
				default:
					Logger.Instance?.LogWarning($"Cannot import image with unsupported color space '{img.ColorSpace}'! Attempting to convert to RGB...");
					img.ColorSpace = ColorSpace.RGB;
					break;
			}

			// Create and initialize raw image data object:
			_outRawImage.width = img.Width;
			_outRawImage.height = img.Height;
			{
				Density density = img.Density;
				_outRawImage.dpi = density.Units switch
				{
					DensityUnit.PixelsPerInch => (uint)Math.Round(density.X),
					DensityUnit.PixelsPerCentimeter => (uint)Math.Round(density.X * PPCm2DPI),
					_ => 0,
				};
			}
			_outRawImage.channelCount = img.ChannelCount;
			_outRawImage.isSRgb = false;// img.ColorSpace == ColorSpace.sRGB;		// note: ignoring sRGB appears to be right, pixels seem to be converted on read.

			// Get a collection of all image pixels:
			using IPixelCollection<ushort> pixelColl = img.GetPixelsUnsafe();

			bool useSDR = _importFlags.HasFlag(ImportFlags.SDR) || img.Depth <= 8;

			// Single-channel monochrome image:
			if (_outRawImage.channelCount == 1)
			{
				if (useSDR)
				{
					return ReadPixelData_Monochrome_Byte(pixelColl, _outRawImage);
				}
				else
				{
					//TODO
					return false;
				}
			}
			// Color image:
			else
			{
				if (useSDR)
				{
					return ReadPixelData_Color_Byte(pixelColl, _outRawImage);
				}
				else
				{
					//TODO
					return false;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to import image from stream!", ex);
			_outRawImage = null!;
			return false;
		}
	}

	private static bool ReadPixelData_Monochrome_Byte(IPixelCollection<ushort> _pixelColl, RawImageData _rawImage)
	{
		//TODO
		return false;
	}

	private static bool ReadPixelData_Color_Byte(IPixelCollection<ushort> _pixelColl, RawImageData _rawImage)
	{
		_rawImage.pixelData_RgbaByte = new RgbaByte[_rawImage.PixelCount];

		// RGB24:
		if (_rawImage.channelCount == 2)
		{
			return ReadPixelData_R8G8(_pixelColl, _rawImage);
		}
		// RGB24:
		else if (_rawImage.channelCount == 3)
		{
			return ReadPixelData_RGB24(_pixelColl, _rawImage);
		}
		// RGBA32:
		else if (_rawImage.channelCount == 4)
		{
			return ReadPixelData_RGBA32(_pixelColl, _rawImage);
		}

		return false;
	}

	private static bool ReadPixelData_R8G8(IPixelCollection<ushort> _pixelColl, RawImageData _rawImage)
	{
		int width = (int)_rawImage.width;
		int height = (int)_rawImage.height;

		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				IPixel<ushort> pixel = _pixelColl[x, y]!;
				byte r = (byte)pixel.GetChannel(0);
				byte g = (byte)pixel.GetChannel(1);

				int dstIdx = y * width + x;
				_rawImage.pixelData_RgbaByte![dstIdx] = new RgbaByte(r, g, 0, 0xFF);
			}
		}
		return true;
	}

	private static bool ReadPixelData_RGB24(IPixelCollection<ushort> _pixelColl, RawImageData _rawImage)
	{
		int width = (int)_rawImage.width;
		int height = (int)_rawImage.height;

		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				IPixel<ushort> pixel = _pixelColl[x, y]!;
				byte r = (byte)pixel.GetChannel(0);
				byte g = (byte)pixel.GetChannel(1);
				byte b = (byte)pixel.GetChannel(2);

				int dstIdx = y * width + x;
				_rawImage.pixelData_RgbaByte![dstIdx] = new RgbaByte(r, g, b, 0xFF);
			}
		}
		return true;
	}

	private static bool ReadPixelData_RGBA32(IPixelCollection<ushort> _pixelColl, RawImageData _rawImage)
	{
		int width = (int)_rawImage.width;
		int height = (int)_rawImage.height;

		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				IPixel<ushort> pixel = _pixelColl[x, y]!;
				byte r = (byte)pixel.GetChannel(0);
				byte g = (byte)pixel.GetChannel(1);
				byte b = (byte)pixel.GetChannel(2);
				byte a = (byte)pixel.GetChannel(3);

				int dstIdx = y * width + x;
				_rawImage.pixelData_RgbaByte![dstIdx] = new RgbaByte(r, g, b, a);
			}
		}
		return true;
	}

	public IEnumerator<ResourceHandle> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream)
	{
		yield break;
	}

	#endregion
}
