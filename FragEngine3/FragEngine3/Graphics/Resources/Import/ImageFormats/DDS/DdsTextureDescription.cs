using FragEngine3.EngineCore;
using Veldrid;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public struct DdsTextureDescription
{
	#region Fields

	public uint width;
	public uint height;
	public uint depth;
	public uint rowByteSize;
	public uint pixelByteSize;
	public uint channelCount;
	public uint arraySize;
	public uint mipMapCount;

	public bool isCompressed;
	public uint compressionBlockSize;

	public bool isSubSampled;
	public bool isRgbColorSpace;
	public bool isLinearColorSpace;

	public Format dxgiFormat;
	public PixelFormat pixelFormat;
	public TextureType textureType;

	private static readonly DdsTextureDescription INVALID_DESC = new()
	{
		width = 0,
		height = 0,
		depth = 0,
		rowByteSize = 0,
		pixelByteSize = 0,
		channelCount = 0,
		arraySize = 0,
		mipMapCount = 0,

		isCompressed = false,
		compressionBlockSize = 0,

		isSubSampled = false,
		isRgbColorSpace = false,
		isLinearColorSpace = false,

		dxgiFormat = Format.Unknown,
		pixelFormat = PixelFormat.R8_UNorm,
		textureType = TextureType.Texture1D,
	};

	#endregion
	#region Properties

	public readonly bool IsTextureArray => arraySize > 1;

	public static DdsTextureDescription Invalid => INVALID_DESC;

	#endregion
	#region Methods

	public readonly bool IsValid()
	{
		bool result =
			width > 0 &&
			height > 0 &&
			depth > 0 &&
			rowByteSize > 0 &&
			channelCount > 0 && channelCount <= 4 &&
			arraySize > 0 &&
			dxgiFormat != Format.Unknown;

		if (result)
		{
			result &= textureType switch
			{
				TextureType.Texture1D => height == 1 && depth == 1,
				TextureType.Texture2D => depth == 1,
				_ => true,
			};
			if (isCompressed)
			{
				result &= compressionBlockSize > 0;
			}
		}
		return result;
	}

	public static Format GetDxgiSurfaceFormat(DdsFileHeader _fileHeader, DdsDxt10Header? _dxt10Header)
	{
		// If DX10 header is present, use format straight from there:
		if (_dxt10Header is not null && _dxt10Header.dxgiFormat != Format.Unknown)
		{
			return _dxt10Header.dxgiFormat;
		}

		// If format is given as FourCC, try parsing that:
		if (_fileHeader.pixelFormat.flags == DdsPixelFormatFlags.FourCC && _fileHeader.pixelFormat.fourCC != DdsPixelFormat.FOURCC_DXT10)
		{
			if (ImageFormatFourCCParser.TryParseFourCC(_fileHeader.pixelFormat.fourCC, out Format format))
			{
				return format;
			}
		}

		//TODO: If no valid FourCC, interprete format from description.

		return Format.Unknown;  //TEMP
	}

	public static uint CalculatePitch(DdsFileHeader _fileHeader, DdsDxt10Header? _dxt10Header)
	{
		Format format = GetDxgiSurfaceFormat(_fileHeader, _dxt10Header);
		if (format == Format.Unknown)
		{
			return 0;
		}

		// Block-compressed formats:
		if (format.IsCompressed())
		{
			uint blockSize = format.GetCompressionBlockSize();
			return Math.Max((_fileHeader.width + 3) / 4, 1) * blockSize;
		}

		// 2-channel-alternating and 4:2:2 sampled formats:
		if (format == Format.R8G8_B8G8_UNorm ||
			format == Format.G8R8_G8B8_UNorm ||
			format == Format.YUY2)
		{
			return ((_fileHeader.width + 1) >> 1) * 4;
		}

		// Other formats:
		uint bitsPerPixel = (uint)format.GetBitsPerPixel();
		return (_fileHeader.width * bitsPerPixel + 7) / 8;
	}

	public static bool TryCreateDescription(DdsFileHeader _fileHeader, DdsDxt10Header? _dxt10Header, out DdsTextureDescription _outDescription)
	{
		if (_fileHeader is null)
		{
			Logger.Instance?.LogError("Cannot create texture description from null DDS file header!");
			_outDescription = INVALID_DESC;
			return false;
		}
		if (!_fileHeader.IsValid())
		{
			Logger.Instance?.LogError("Cannot create texture description from invalid DDS file header!");
			_outDescription = INVALID_DESC;
			return false;
		}

		_outDescription = new()
		{
			width = _fileHeader.width,
			height = _fileHeader.height,
			depth = _fileHeader.depth,
			arraySize = 1,
			mipMapCount = Math.Max(_fileHeader.mipMapCount, 1),
			rowByteSize = Math.Max(CalculatePitch(_fileHeader, _dxt10Header), 1),
		};

		// For simple raw color formats, read pixel byte size directly:
		if (_fileHeader.pixelFormat.flags == DdsPixelFormatFlags.RGB ||
			_fileHeader.pixelFormat.flags == DdsPixelFormatFlags.YUV ||
			_fileHeader.pixelFormat.flags == DdsPixelFormatFlags.Luminance)
		{
			_outDescription.pixelByteSize = Math.Max((_fileHeader.pixelFormat.rgbBitCount + 7) / 8, 1);
		}

		if (_dxt10Header is not null)
		{
			_outDescription.arraySize = _dxt10Header.IsTextureCube ? 6 : _dxt10Header.arraySize;
			_outDescription.dxgiFormat = _dxt10Header.dxgiFormat;
		}
		else
		{
			_outDescription.dxgiFormat = GetDxgiSurfaceFormat(_fileHeader, _dxt10Header);
			if (_outDescription.dxgiFormat == Format.Unknown)
			{
				Logger.Instance?.LogError("Could not determine texture's DXGI surface format!");
				return false;
			}
		}

		// Set properties that are inherent to DXGI format:
		_outDescription.pixelByteSize = (uint)Math.Max((_outDescription.dxgiFormat.GetBitsPerPixel() + 7) / 8, 1);
		_outDescription.isCompressed = _outDescription.dxgiFormat.IsCompressed();
		_outDescription.isLinearColorSpace = !_outDescription.dxgiFormat.IsSRGB();

		// Try to determine pixel format:


		return true;
	}

	public readonly uint CalculateTotalByteSize()
	{
		if (isCompressed)
		{
			return CalculateTotalByteSize_Compressed();
		}
		else
		{
			return CalculateTotalByteSize_Uncompressed();
		}
	}

	private readonly uint CalculateTotalByteSize_Compressed()
	{
		uint w = Math.Max(width, 1);
		uint h = Math.Max(height, 1);
		uint d = Math.Max(depth, 1);

		uint blockByteSize = pixelFormat.GetCompressionBlockSize();

		uint layerByteSize = 0;
		for (int mipMapIdx = 0; mipMapIdx < mipMapCount; ++mipMapIdx)
		{
			uint blockCountX = Math.Max((w + 3) / 4, 1);
			uint blockCountY = Math.Max((h + 3) / 4, 1);

			uint mipMapByteSize = blockCountX * blockCountY * blockByteSize;
			layerByteSize += mipMapByteSize;

			w = Math.Max(w / 2, 1);
			h = Math.Max(h / 2, 1);
			d = Math.Max(d / 2, 1);
		}

		uint totalByteSize = layerByteSize * Math.Max(arraySize, 1);
		return totalByteSize;
	}

	private readonly uint CalculateTotalByteSize_Uncompressed()
	{
		uint w = Math.Max(width, 1);
		uint h = Math.Max(height, 1);
		uint d = Math.Max(depth, 1);

		uint layerPixelCount = 0;
		for (int mipMapIdx = 0; mipMapIdx < mipMapCount; ++mipMapIdx)
		{
			uint mipMapPixelCount = w * h * d;
			layerPixelCount += mipMapPixelCount;

			w = Math.Max(w / 2, 1);
			h = Math.Max(h / 2, 1);
			d = Math.Max(d / 2, 1);
		}

		uint totalPixelCount = layerPixelCount * Math.Max(arraySize, 1);
		uint totalByteSize = totalPixelCount * pixelByteSize;
		return totalByteSize;
	}

	#endregion
}
