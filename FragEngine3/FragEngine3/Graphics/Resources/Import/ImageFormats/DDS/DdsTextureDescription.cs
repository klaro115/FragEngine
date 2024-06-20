using FragEngine3.EngineCore;
using Veldrid;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public struct DdsTextureDescription
{
	#region Fields

	public ushort width;
	public ushort height;
	public ushort depth;
	public ushort rowByteSize;
	public ushort pixelByteSize;
	public ushort channelCount;
	public uint arraySize;
	public uint mipMapCount;

	public bool isCompressed;
	public ushort compressionBlockSize;

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
			width = (ushort)_fileHeader.width,
			height = (ushort)_fileHeader.height,
			depth = (ushort)_fileHeader.depth,
			arraySize = 1,
			mipMapCount = _fileHeader.mipMapCount,
		};

		if (_dxt10Header is not null)
		{
			_outDescription.arraySize = _dxt10Header.arraySize;

			_outDescription.dxgiFormat = _dxt10Header.dxgiFormat;
			//...
		}
		else
		{
			_outDescription.dxgiFormat = GetDxgiSurfaceFormat(_fileHeader, _dxt10Header);
			if (_outDescription.dxgiFormat == Format.Unknown)
			{
				Logger.Instance?.LogError("Could not determine texture's DXGI surface format!");
				return false;
			}

			//TODO: Parse format from format and pixel description, if possible.
		}

		return true;
	}

	#endregion
}
