﻿using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data;

/// <summary>
/// An image's data, with limited data description and support for single-channel monochromatic and RGBA color images.<para/>
/// NOTE: This is purely a CPU-side representation of an image's pixel data, and not a texture asset that can be used on the GPU.
/// This type is used as an intermediary when importing images, or for processing image data on the CPU and using system memory.
/// </summary>
public sealed class RawImageData
{
	#region Types

	private readonly struct MipMapLevelSize(int _resX, int _resY, int _pixelStartIdx, int _pixelCount)
	{
		public readonly int resX = _resX;
		public readonly int resY = _resY;
		public readonly int pixelStartIdx = _pixelStartIdx;
		public readonly int pixelCount = _pixelCount;
	}

	private delegate T FuncAverage2x2<T>(T _a, T _b, T _c, T _d) where T : unmanaged;

	#endregion
	#region Fields

	// Image & format parameters:
	public uint width = 0;
	public uint height = 0;
	public uint dpi = 0;
	public uint bitsPerPixel = 32;
	public uint channelCount = 4;
	public bool isSRgb = true;
	public byte mipmaps = 0;

	// Pixel data arrays:
	public byte[]? pixelData_MonoByte = null;
	public float[]? pixelData_MonoFloat = null;
	public RgbaByte[]? pixelData_RgbaByte = null;
	public RgbaFloat[]? pixelData_RgbaFloat = null;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the total number of pixels.
	/// </summary>
	public uint PixelCount => width * height;
	/// <summary>
	/// Gets the actual number of data points (pixels) provided for this image.
	/// If the number is less than '<see cref="PixelCount"/>', or if it's zero, the image is invalid.
	/// </summary>
	public uint DataCount
	{
		get
		{
			Array? pixelData = GetPixelDataArray();
			return pixelData is not null ? Math.Min((uint)pixelData.Length, PixelCount) : 0u;
		}
	}

	/// <summary>
	/// Gets the maximum byte size of the image's raw uncompressed pixel data.
	/// </summary>
	public uint MaxDataByteSize => width * height * (bitsPerPixel / 8);

	/// <summary>
	/// Gets an invalid raw image object, with all zero dimensions and null data arrays.
	/// </summary>
	public static RawImageData Invalid => new()
	{
		width = 0,
		height = 0,
		dpi = 0,
		bitsPerPixel = 0,
		channelCount = 0,
		isSRgb = false,
		mipmaps = 0,

		pixelData_MonoByte = null,
		pixelData_MonoFloat = null,
		pixelData_RgbaByte = null,
		pixelData_RgbaFloat = null,
	};

	#endregion
	#region Methods

	/// <summary>
	/// Checks whether the data contained in this raw image is valid.
	/// </summary>
	/// <returns>True if valid, false if null or nonsensical.</returns>
	public bool IsValid()
	{
		Array? pixelData = GetPixelDataArray();
		return pixelData is not null && pixelData.Length >= PixelCount;
	}

	public Array? GetPixelDataArray() => bitsPerPixel switch
	{
		8 => pixelData_MonoByte,
		32 => channelCount != 1 ? pixelData_RgbaByte : pixelData_MonoFloat,
		128 => pixelData_RgbaFloat,
		_ => null,
	};

	/// <summary>
	/// Gets the pixel format that best describes the data contained within this raw image.
	/// Using this format, the pixel data can be uploaded to a texture resource as-is.
	/// </summary>
	/// <returns></returns>
	public PixelFormat GetPixelFormat()
	{
		switch (bitsPerPixel)
		{
			case 8:
				return PixelFormat.R8_UNorm;
			case 32:
				if (channelCount != 1)
				{
					return isSRgb ? PixelFormat.R8_G8_B8_A8_UNorm_SRgb : PixelFormat.R8_G8_B8_A8_UNorm;
				}
				else
				{
					return PixelFormat.R32_Float;
				}
			case 128:
				return PixelFormat.R32_G32_B32_A32_Float;
			default:
				return PixelFormat.B8_G8_R8_A8_UNorm;
		}
	}

	/// <summary>
	/// Reverse row order of the image, therefore mirroring its content vertically.<para/>
	/// NOTE: This operation is done as in-place as possible, using a small single-row buffer as intermediate storage.<para/>
	/// IMPORT: Use the import flag "mirrorY" to perform this conversion on texture import.</summary>
	/// </summary>
	/// <returns>True if the image was mirrored vertically, false if the image was invalid.</returns>
	public bool MirrorVertically()
	{
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot mirror invalid raw image along Y-axis!");
			return false;
		}

		uint halfHeight = height / 2;
		switch (bitsPerPixel)
		{
			case 8:
				return MirrorVerticallyTyped(pixelData_MonoByte!);
			case 32:
				return channelCount != 1
					? MirrorVerticallyTyped(pixelData_RgbaByte!)
					: MirrorVerticallyTyped(pixelData_MonoFloat!);
			case 128:
				return MirrorVerticallyTyped(pixelData_RgbaFloat!);
			default:
				Logger.Instance?.LogError($"Cannot mirror raw image with unsupported pixel format! (bitsPerPixel={bitsPerPixel}, channelCount={channelCount})");
				return false;
		}


		// Local helper method for mirroring pixel data arrays of arbitrary pixel size and layout:
		bool MirrorVerticallyTyped<T>(T[] _pixelData)
		{
			T[] rowBuffer = new T[width];
			try
			{
				for (uint y = 0; y < halfHeight; ++y)
				{
					long lowLineStartIdx = y * width;
					long highLineStartIdx = (height - y - 1) * width;

					Array.Copy(_pixelData, lowLineStartIdx, rowBuffer, 0, width);                   // low => buffer
					Array.Copy(_pixelData, highLineStartIdx, _pixelData, lowLineStartIdx, width);   // high => low
					rowBuffer.CopyTo(_pixelData, highLineStartIdx);                                 // buffer => high
				}
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to mirror raw image! (bitsPerPixel={bitsPerPixel}, channelCount={channelCount})", ex);
				return false;
			}
		}
	}

	/// <summary>
	/// Converts a normal map image between Direct3D and OpenGL standards.<para/>
	/// HINT: In order to convert between both standards, the value of the green color channel of each pixel is inverted, substracting it from 
	/// the maximum saturation value. This method will convert from whichever the current standard is to the respective other one; the same
	/// operation works in both directions, so no need to think about what you're currently using. If it looks wrong or normals appear weirdly
	/// mirrored, this should fix it. This function does nothing for single-channel image data.<para/>
	/// IMPORT: Normals are expected to be in OpenGL format. Use the import flag "normalsDX" to perform this conversion on texture import.</summary>
	/// <returns>True if the image was successfully converted, false otherwise.</returns>
	public bool ConvertNormalMapDxAndGL()
	{
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot convert normal map standard of invalid raw image!");
			return false;
		}

		if (pixelData_RgbaByte is not null)
		{
			for (uint i = 0; i < pixelData_RgbaByte.Length; ++i)
			{
				RgbaByte pixel = pixelData_RgbaByte[i];
				pixelData_RgbaByte[i] = new(pixel.R, (byte)(0xFF - pixel.G), pixel.B, pixel.A);
			}
		}
		else if (pixelData_RgbaFloat is not null)
		{
			for (uint i = 0; i < pixelData_RgbaFloat.Length; ++i)
			{
				RgbaFloat pixel = pixelData_RgbaFloat[i];
				pixelData_RgbaFloat[i] = new(pixel.R, 1.0f - pixel.G, pixel.B, pixel.A);
			}
		}
		return true;
	}

	/// <summary>
	/// Increases image size and pads contents with black pixels if the image's dimensions are not power-of-two values.
	/// </summary>
	/// <returns>True if padding was successful, false if the image was invalid.</returns>
	public bool PadSizeToNextPowerOfTwo()
	{
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot pad resolution of invalid raw image to power-of-two values!");
			return false;
		}

		float powerOfTwoX = MathF.Ceiling(MathF.Log2(width));
		float powerOfTwoY = MathF.Ceiling(MathF.Log2(height));
		int po2Width = 1 << (int)powerOfTwoX;
		int po2Height = 1 << (int)powerOfTwoY;

		// Do nothing if dimensions are already powers of two:
		if (width == po2Width && height == po2Height)
		{
			return true;
		}

		int po2PixelCount = po2Width * po2Height;
		int paddingX = (int)(po2Width - width);
		int paddingY = (int)(po2Height - height);

		PadSizeTyped(ref pixelData_MonoByte, (byte)0x00);
		PadSizeTyped(ref pixelData_MonoFloat, 0.0f);
		PadSizeTyped(ref pixelData_RgbaByte, new RgbaByte(0, 0, 0, 0));
		PadSizeTyped(ref pixelData_RgbaFloat, new RgbaFloat(0, 0, 0, 0));

		width = (uint)po2Width;
		height = (uint)po2Height;
		return true;


		// Local helper method for padding pixel data arrays of arbitrary pixel size and layout:
		void PadSizeTyped<T>(ref T[]? _pixels, T _padValue) where T : unmanaged
		{
			if (_pixels is null) return;

			T[] po2Pixels = new T[po2PixelCount];
			for (int y = 0; y < height; ++y)
			{
				int srcStartIdx = y * (int)width;
				int dstStartIdx = y * po2Height;
				Array.Copy(_pixels, srcStartIdx, po2Pixels, dstStartIdx, width);
				Array.Fill(po2Pixels, _padValue, dstStartIdx + (int)width, paddingX);
			}
			if (height < po2Height)
			{
				Array.Fill(po2Pixels, _padValue, (int)PixelCount, paddingY * po2Width);
			}
			_pixels = po2Pixels;
		}
	}

	/// <summary>
	/// Converts all pixels' color space from sRGB to linear. Does nothing if '<see cref="isSRgb"/>' is false.
	/// </summary>
	/// <returns>True if the color space was converted or already in linear space, false if the image was invalid.</returns>
	public bool ConvertSRgbToLinearColorSpace()
	{
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot convert invalid raw image from sRGB to linear color space!");
			return false;
		}

		// Do nothing if original color space isn't sRGB:
		if (!isSRgb)
		{
			return true;
		}

		const float inv255 = 1.0f / 255.0f;

		ConvertSRgbToLinear(pixelData_MonoByte, CvtChannel_Byte);
		ConvertSRgbToLinear(pixelData_MonoFloat, CvtChannel_Float);
		ConvertSRgbToLinear(pixelData_RgbaByte, CvtRgbaByte);
		ConvertSRgbToLinear(pixelData_RgbaFloat, CvtRgbaFloat);
		return true;


		static void ConvertSRgbToLinear<TColor>(TColor[]? _pixels, Func<TColor, TColor> _funcCvtChannel) where TColor : unmanaged
		{
			if (_pixels is null) return;

			for (uint i = 0; i < _pixels.Length; ++i)
			{
				_pixels[i] = _funcCvtChannel(_pixels[i]);
			}
		}
		static RgbaByte CvtRgbaByte(RgbaByte _srgb)
		{
			return new(
				CvtChannel_Byte(_srgb.R),
				CvtChannel_Byte(_srgb.G),
				CvtChannel_Byte(_srgb.B),
				_srgb.A);
		}
		static RgbaFloat CvtRgbaFloat(RgbaFloat _srgb)
		{
			return new(
				CvtChannel_Float(_srgb.R),
				CvtChannel_Float(_srgb.G),
				CvtChannel_Float(_srgb.B),
				_srgb.A);
		}
		static byte CvtChannel_Byte(byte _srgb)
		{
			float x = _srgb * inv255;
			x = MathF.Pow(x, 2.2f);
			return (byte)Math.Min(x * 255.0f, 255.0f);
		}
		static float CvtChannel_Float(float _srgb)
		{
			return MathF.Pow(_srgb, 2.2f);
		}
	}

	/// <summary>
	/// Crops out a rectangular region of the image.
	/// </summary>
	/// <param name="_startX">Horizontal starting point for the cropped region, in pixels.</param>
	/// <param name="_startY">Vertical starting point for the cropped region, in pixels.</param>
	/// <param name="_cropWidth">Horizontal size of the cropped region, in pixels. If the cropped region exceeds
	/// horizontal boundaries of the source image, the width is clamped to only include valid pixel space.</param>
	/// <param name="_cropHeight">Vertical size of the cropped region, in pixels. If the cropped region exceeds
	/// vertical boundaries of the source image, the height is clamped to only include valid pixel space.</param>
	/// <param name="_outCroppedImage">Outputs a new raw image object containing a copy of the cropped image area.</param>
	/// <returns>True if a copy of the specified region could be made, false if the image is invalid or the cropping
	/// region is entirely out of bounds.</returns>
	public bool Crop(uint _startX, uint _startY, uint _cropWidth, uint _cropHeight, out RawImageData _outCroppedImage)
	{
		if (!IsValid())
		{
			Logger.Instance?.LogError("Cannot crop from invalid raw image!");
			_outCroppedImage = Invalid;
			return false;
		}

		if (_startX > width || _startY > height)
		{
			Logger.Instance?.LogError("Crop region lies entirely out of range!");
			_outCroppedImage = Invalid;
			return false;
		}

		uint maxX = Math.Min(_startX + _cropWidth, width);
		uint maxY = Math.Min(_startY + _cropHeight, height);
		_cropWidth = maxX - _startX;
		_cropHeight = maxY - _startY;

		_outCroppedImage = new RawImageData()
		{
			width = _cropWidth,
			height = _cropHeight,
			dpi = dpi,
			bitsPerPixel = bitsPerPixel,
			channelCount = channelCount,
			isSRgb = isSRgb,

			pixelData_MonoByte = CropPixels(pixelData_MonoByte),
			pixelData_MonoFloat = CropPixels(pixelData_MonoFloat),
			pixelData_RgbaByte = CropPixels(pixelData_RgbaByte),
			pixelData_RgbaFloat = CropPixels(pixelData_RgbaFloat),
		};
		return true;


		// Local helper method for copying a rectangle of pixels of arbitrary pixel size and layout:
		T[]? CropPixels<T>(T[]? _pixels) where T : unmanaged
		{
			if (_pixels is null) return null;

			T[] cropped = new T[_cropWidth * _cropHeight];
			for (uint y = _startY;  y < _cropHeight; y++)
			{
				uint srcStartIdx = y * width + _startX;
				uint dstStartIdx = y * _cropWidth;
				Array.Copy(_pixels, (int)srcStartIdx, cropped, (int)dstStartIdx, _cropWidth);
			}
			return cropped;
		}
	}

	/// <summary>
	/// Generate mip map pixel data and append it to the image's pixels.
	/// </summary>
	/// <param name="_levels">The number of LOD levels we want. For values of 1 or lower, no mip maps are generated.</param>
	/// <returns>True if mip maps were created successfully, false if any of the pixel data arrays are invalid.</returns>
	public bool GenerateMipMaps(int _levels)
	{
		if (_levels <= 1)
		{
			return true;
		}
		if (mipmaps > 1)
		{
			Logger.Instance?.LogWarning("Raw image data already has mipmaps; are you sure you want to create them twice?");
		}

		// Do not exceed maximum number of LODs:
		int maxLevelCount = (int)MathF.Log2(Math.Max(width, height));
		_levels = Math.Min(_levels, maxLevelCount);
		if (_levels <= 1)
		{
			return true;
		}

		// Calculate size and offset of each mip level:
		MipMapLevelSize[] levelSizes = new MipMapLevelSize[_levels];
		int mipmappedPixelCount = 0;
		{
			int resX = (int)width;
			int resY = (int)height;

			for (uint i = 0; i < _levels; i++)
			{
				int levelStartIdx = mipmappedPixelCount;
				int levelPixelCount = resX * resY;
				mipmappedPixelCount += levelPixelCount;

				levelSizes[i] = new(resX, resY, levelStartIdx, levelPixelCount);

				resX = Math.Max(resX / 2, 1);
				resY = Math.Max(resY / 2, 1);
			}
		}

		bool success = true;

		// Iteratively generate pixel data for each LOD:
		if (pixelData_MonoByte is not null)
		{
			success &= GenerateMipMaps(_levels, ref pixelData_MonoByte, levelSizes, mipmappedPixelCount, FuncAverage_MonoByte);
		}
		if (pixelData_MonoFloat is not null)
		{
			success &= GenerateMipMaps(_levels, ref pixelData_MonoFloat, levelSizes, mipmappedPixelCount, FuncAverage_MonoFloat);
		}
		if (pixelData_RgbaByte is not null)
		{
			success &= GenerateMipMaps(_levels, ref pixelData_RgbaByte, levelSizes, mipmappedPixelCount, FuncAverage_RgbaByte);
		}
		if (pixelData_RgbaFloat is not null)
		{
			success &= GenerateMipMaps(_levels, ref pixelData_RgbaFloat, levelSizes, mipmappedPixelCount, FuncAverage_RgbaFloat);
		}

		mipmaps = (byte)_levels;
		return true;


		// Helper functions for calculating the average color value of a 2x2 pixel block:
		static byte FuncAverage_MonoByte(byte _a, byte _b, byte _c, byte _d)
		{
			return (byte)((_a + _b + _c + _d) >> 2);
		}
		static float FuncAverage_MonoFloat(float _a, float _b, float _c, float _d)
		{
			return (_a + _b + _c + _d) * 0.25f;
		}
		static RgbaByte FuncAverage_RgbaByte(RgbaByte _a, RgbaByte _b, RgbaByte _c, RgbaByte _d)
		{
			return new(
				(byte)((_a.R + _b.R + _c.R + _d.R) >> 2),
				(byte)((_a.G + _b.G + _c.G + _d.G) >> 2),
				(byte)((_a.B + _b.B + _c.B + _d.B) >> 2),
				(byte)((_a.A + _b.A + _c.A + _d.A) >> 2));
		}
		static RgbaFloat FuncAverage_RgbaFloat(RgbaFloat _a, RgbaFloat _b, RgbaFloat _c, RgbaFloat _d)
		{
			return new(
				(_a.R + _b.R + _c.R + _d.R) * 0.25f,
				(_a.G + _b.G + _c.G + _d.G) * 0.25f,
				(_a.B + _b.B + _c.B + _d.B) * 0.25f,
				(_a.A + _b.A + _c.A + _d.A) * 0.25f);
		}
	}

	private bool GenerateMipMaps<T>(int _levels, ref T[] _pixels, MipMapLevelSize[] _levelSizes, int _mipmappedPixelCount, FuncAverage2x2<T> _funcAverage) where T : unmanaged
	{
		if (_levels <= 1)
		{
			return true;
		}
		if (mipmaps > 1)
		{
			Logger.Instance?.LogWarning("Raw image data already has mipmaps; are you sure you want to create them twice?");
		}
		if (_pixels is null || _pixels.Length < width * height)
		{
			Logger.Instance?.LogError("Pixel data array has invalid size for mip map calculation!");
			return false;
		}

		T[] mipmappedPixels = new T[_mipmappedPixelCount];
		_pixels.CopyTo(mipmappedPixels, 0);

		// For each pixel in smaller level, average colors of a 2x2 quad on the next larger level:
		int prevResX = (int)width;
		Span<T> prevLevelPixels = _pixels.AsSpan(0, (int)(width * height));
		for (uint i = 1; i < _levels; i++)
		{
			MipMapLevelSize levelSize = _levelSizes[i];
			Span<T> levelPixels = mipmappedPixels.AsSpan(levelSize.pixelStartIdx, levelSize.pixelCount);

			int resX = levelSize.resX;
			int resY = levelSize.resY;
			for (int y = 0; y < resY; ++y)
			{
				int srcY0 = 2 * y;
				int srcY1 = srcY0 + 1;
				srcY0 *= prevResX;
				srcY1 *= prevResX;

				for (int x = 0; x < resX; ++x)
				{
					int srcX0 = 2 * x;
					int srcX1 = srcX0 + 1;

					int dstIndex = y * resX + x;

					// Sample 2x2 pixels from larger level:
					T srcPixel00 = prevLevelPixels[srcY0 + srcX0];
					T srcPixel01 = prevLevelPixels[srcY0 + srcX1];
					T srcPixel10 = prevLevelPixels[srcY1 + srcX0 ];
					T srcPixel11 = prevLevelPixels[srcY1 + srcX1];

					// Calculate arithmetic average of 4 pixels to form smaller level's pixel value:
					T dstPixel = _funcAverage(srcPixel00, srcPixel01, srcPixel10, srcPixel11);
					levelPixels[dstIndex] = dstPixel;
				}
			}

			prevResX = resX;
			prevLevelPixels = levelPixels;
		}

		_pixels = mipmappedPixels;
		return true;
	}

	public TextureDescription CreateTextureDescription()
	{
		return new TextureDescription(width, height, 1, 1, 1, GetPixelFormat(), TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1);
	}

	public override string ToString()
	{
		return $"RawImageData (Resolution: {width}x{height}, Format: {GetPixelFormat()}, Size: {MaxDataByteSize} bytes)";
	}

	#endregion
}
