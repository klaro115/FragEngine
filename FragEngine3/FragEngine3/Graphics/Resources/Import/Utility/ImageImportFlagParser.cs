﻿using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Utility;

namespace FragEngine3.Graphics.Resources.Import.Utility;

public static class ImageImportFlagParser
{
	#region Methods

	public static bool ApplyImportFlags(RawImageData _imageData, string? _importFlags)
	{
		if (_imageData is null || !_imageData.IsValid())
		{
			Logger.Instance?.LogError("Cannot apply import flags to null or invalid raw image data!");
			return false;
		}
		if (string.IsNullOrEmpty(_importFlags))
		{
			return true;
		}

		bool success = true;

		// Flip image row order: (mirrors contents vertically)
		if (FindFlag(ImportFlagsConstants.IMG_MIRROR_Y, out _))
		{
			success &= _imageData.MirrorVertically();
		}
		// Normals are using D3D standard, convert to OpenGL standard: (inverts green color value)
		if (FindFlag(ImportFlagsConstants.IMG_NORMALS_D3D_CONVENTION, out _))
		{
			success &= _imageData.ConvertNormalMapDxAndGL();
		}
		// Pixel values are in sRGB color space, convert to linear color.
		if (FindFlag(ImportFlagsConstants.IMG_CONVERT_SRGB_TO_LINEAR, out _))
		{
			success &= _imageData.ConvertSRgbToLinearColorSpace();
		}
		// Dimensions need padding to next higher power-of-two value: (pads content with black pixels)
		if (FindFlag(ImportFlagsConstants.IMG_PAD_NEXT_POWER_OF_2, out _))
		{
			success &= _imageData.PadSizeToNextPowerOfTwo();
		}

		//...

		// Generate and append mip maps to the image's buffer:
		if (FindFlag(ImportFlagsConstants.IMG_GENERATE_MIPMAPS, out int startIdx))
		{
			int mipmapEndIdx = startIdx + ImportFlagsConstants.IMG_GENERATE_MIPMAPS.Length;

			if (_importFlags.Length <= mipmapEndIdx + 1 ||
				_importFlags[mipmapEndIdx] != '=' ||
				!_importFlags.ReadNextInteger(mipmapEndIdx + 1, out int mipmapCount, out _))
			{
				mipmapCount = (int)MathF.Log2(Math.Max(_imageData.width, _imageData.height));
			}

			success &= _imageData.GenerateMipMaps(mipmapCount);
		}

		return success;


		bool FindFlag(string _query, out int _outStartIdx)
		{
			_outStartIdx = _importFlags.IndexOf(_query, StringComparison.Ordinal);
			return _outStartIdx >= 0;
		}
	}

	#endregion
}
