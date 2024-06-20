using System.Collections.Frozen;
using FragEngine3.Resources;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats;

public static class ImageFormatFourCCParser
{
	#region Fields

	private static readonly FrozenDictionary<FourCC, Format> fourCCMap = new KeyValuePair<FourCC, Format>[]
	{
		// RGB:

		// ... (missing regular RGB formats, but researching FourCC codes is a poorly documented retro-nightmare)

		// Packed RGB 4:2:2
		new(new("RGBG"), Format.G8R8_G8B8_UNorm),
		new(new("GRGB"), Format.R8G8_B8G8_UNorm),

		// YUV:

		// Packed YUV 4:2:2
		new(new("Y210"), Format.Y210),
		new(new("Y216"), Format.Y216),
		new(new("YUY2"), Format.YUY2),
		// Packed YUV 4:4:4
		new(new("AYUV"), Format.AYUV),
		new(new("Y410"), Format.Y410),
		new(new("Y416"), Format.Y416),

		// Planar YUV 4:2:0
		new(new("NV11"), Format.NV11),
		new(new("NV12"), Format.NV12),
		new(new("P010"), Format.P010),
		new(new("P016"), Format.P016),
		// Planar YUV 4:2:2
		new(new("P208"), Format.P208),

		// COMPRESSED:

		// DXT and old BC formats:
		new(new("DXT1"), Format.BC1_UNorm),
		new(new("DXT2"), Format.BC2_UNorm),
		new(new("DXT3"), Format.BC2_UNorm),
		new(new("DXT4"), Format.BC3_UNorm),
		new(new("DXT5"), Format.BC3_UNorm),

		// Newer BC formats:
		new(new("BC4U"), Format.BC4_UNorm),
		new(new("BC4S"), Format.BC4_SNorm),
		new(new("ATI2"), Format.BC5_UNorm),
		new(new("BC5S"), Format.BC5_SNorm),
	}.ToFrozenDictionary();

	#endregion
	#region Methods

	public static bool TryParseFourCC(FourCC _fourCC, out Format _outFormat)
	{
		if (!_fourCC.IsValid())
		{
			_outFormat = Format.Unknown;
			return false;
		}

		return fourCCMap.TryGetValue(_fourCC, out _outFormat);
	}

	#endregion
}
