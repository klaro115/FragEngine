namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

/// <summary>
/// Flags of features contained in the image data.<para/>
/// According to Microsoft documentation, some people don't bother writing these flags correctly.
/// Therefore the importer should derive presence of the flags' features directly from the data.
/// </summary>
[Flags]
public enum DdsFileHeaderFlags : uint
{
	Caps			= 1,
	Height			= 2,
	Width			= 4,
	Pitch			= 8,
	PixelFormat		= 0x1000u,
	MipMapCount		= 0x20000u,
	LinearSize		= 0x80000u,
	Depth			= 0x800000u
}

[Flags]
public enum DdsPixelFormatFlags : uint
{
	AlphaPixels		= 1,
	Alpha			= 2,
	FourCC			= 4,
	RGB				= 0x40u,
	YUV				= 0x200u,
	Luminance		= 0x20000u,
}

public enum DdsAlphaMode : uint
{
	Unknown			= 0,
	Straight		= 1,
	Premultiplied	= 2,	// Premultiplied alpha, supported by formats "DX2" and "DX4".
	Opaque			= 3,
	Custom			= 4,	// Alpha channel is not alpha/transparency, but used as data/color value.
}
