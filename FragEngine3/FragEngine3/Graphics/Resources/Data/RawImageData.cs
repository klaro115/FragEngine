using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data
{
	public sealed class RawImageData
	{
		#region Fields

		// Image & format parameters:
		public uint width = 0;
		public uint height = 0;
		public uint dpi = 0;
		public uint bitsPerPixel = 32;
		public uint channelCount = 4;
		public bool isSrgb = true;

		// Pixel data arrays:
		public byte[]? pixelData_MonoByte = null;
		public float[]? pixelData_MonoFloat = null;
		public RgbaByte[]? pixelData_RgbaByte = null;
		public RgbaFloat[]? pixelData_RgbaFloat = null;

		#endregion
		#region Properties

		public uint PixelCount => width * height;
		public uint DataCount
		{
			get
			{
				Array? pixelData = GetPixelDataArray();
				return pixelData != null ? Math.Min((uint)pixelData.Length, PixelCount) : 0u;
			}
		}

		public uint MaxDataByteSize => width * height * (bitsPerPixel / 8);

		#endregion
		#region Methods

		public bool IsValid()
		{
			Array? pixelData = GetPixelDataArray();
			return pixelData != null && pixelData.Length >= PixelCount;
		}

		public Array? GetPixelDataArray() => bitsPerPixel switch
		{
			8 => pixelData_MonoByte,
			32 => channelCount != 1 ? pixelData_RgbaByte : pixelData_MonoFloat,
			128 => pixelData_RgbaFloat,
			_ => null,
		};

		public PixelFormat GetPixelFormat()
		{
			switch (bitsPerPixel)
			{
				case 8:
					return PixelFormat.R8_UNorm;
				case 32:
					if (channelCount != 1)
					{
						return isSrgb ? PixelFormat.R8_G8_B8_A8_UNorm_SRgb : PixelFormat.R8_G8_B8_A8_UNorm;
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

			if (pixelData_RgbaByte != null)
			{
				for (uint i = 0; i < pixelData_RgbaByte.Length; ++i)
				{
					RgbaByte pixel = pixelData_RgbaByte[i];
					pixelData_RgbaByte[i] = new(pixel.R, (byte)(0xFF - pixel.G), pixel.B, pixel.A);
				}
			}
			else if (pixelData_RgbaFloat != null)
			{
				for (uint i = 0; i < pixelData_RgbaFloat.Length; ++i)
				{
					RgbaFloat pixel = pixelData_RgbaFloat[i];
					pixelData_RgbaFloat[i] = new(pixel.R, 1.0f - pixel.G, pixel.B, pixel.A);
				}
			}
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
}
