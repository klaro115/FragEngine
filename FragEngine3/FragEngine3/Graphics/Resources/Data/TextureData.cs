using Veldrid;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Data;

public sealed class TextureData
{
	#region Fields

	// Texture parameters:
	public uint width = 0;
	public uint height = 0;
	public uint depth = 0;
	public uint arraySize = 0;
	public bool isCubemap = false;
	public uint dpi = 0;
	public uint bitsPerPixel = 32;
	public uint channelCount = 4;
	public bool isSRgb = true;
	public byte mipmaps = 0;

	// Format:
	public PixelFormat pixelFormat = PixelFormat.R8_UNorm;
	public Format dxgiFormat = Format.Unknown;

	// Data arrays:
	public byte[]? pixelData = null;

	#endregion
	#region Properties

	public uint BasePixelCount => width * Math.Max(height, 1) * Math.Max(depth, 1);
	public uint DataByteSize => pixelData is not null ? (uint)pixelData.Length : 0u;

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool result =
			width > 0 &&
			height > 0 &&
			depth > 0 &&
			arraySize > 0 &&
			bitsPerPixel > 0 &&
			channelCount > 0 &&
			dxgiFormat != Format.Unknown &&
			pixelData is not null;
		return result;
	}

	public TextureType GetTextureType()
	{
		if (depth > 1)
		{
			return TextureType.Texture3D;
		}
		else if (height > 1)
		{
			return TextureType.Texture2D;
		}
		else
		{
			return TextureType.Texture1D;
		}
	}

	public TextureDescription CreateTextureDescription()
	{
		uint w = Math.Max(width, 1);
		uint h = Math.Max(height, 1);
		uint d = Math.Max(depth, 1);

		uint mipMapCount = Math.Max((uint)mipmaps, 1);
		uint layerCount = isCubemap ? 6 : Math.Max(arraySize, 1);

		TextureType texType = GetTextureType();

		return new TextureDescription(w, h, d, mipMapCount, layerCount, pixelFormat, TextureUsage.Sampled, texType, TextureSampleCount.Count1);
	}

	public override string ToString()
	{
		return $"RawImageData (Resolution: {width}x{height}x{depth}, Layers: {arraySize}, Format: {pixelFormat}, Size: {DataByteSize} bytes)";
	}

	#endregion
}
