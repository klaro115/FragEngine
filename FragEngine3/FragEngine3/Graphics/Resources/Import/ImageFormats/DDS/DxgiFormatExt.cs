using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public static class DxgiFormatExt
{
	#region Methods

	/// <summary>
	/// Gets whether this format is block-compressed (BC).
	/// </summary>
	/// <param name="_format">This DXGI surface format.</param>
	/// <returns>True if the format is block-compressed, false otherwise.</returns>
	public static bool IsBlockCompressed(this Format _format)
	{
		return
			(_format >= Format.BC1_Typeless && _format <= Format.BC5_SNorm) ||
			(_format >= Format.BC6H_Typeless && _format <= Format.BC7_UNorm_SRgb);
	}

	/// <summary>
	/// Gets the block size for block-compressed (BC) surface formats.
	/// </summary>
	/// <param name="_format">This DXGI surface format.</param>
	/// <returns>The number of bytes per block, or zero, if the format is not block-compressed.</returns>
	public static uint GetCompressionBlockSize(this Format _format)
	{
		// BC1:
		if (_format >= Format.BC1_Typeless && _format <= Format.BC1_UNorm_SRgb)
		{
			return 8;
		}
		// BC4:
		else if (_format >= Format.BC4_Typeless && _format <= Format.BC4_SNorm)
		{
			return 8;
		}
		// BC2 & BC3:
		else if (_format >= Format.BC2_Typeless && _format <= Format.BC3_UNorm_SRgb)
		{
			return 16;
		}
		// BC5:
		else if (_format >= Format.BC5_Typeless && _format <= Format.BC5_SNorm)
		{
			return 16;
		}
		// BC6H && BC7:
		else if (_format >= Format.BC6H_Typeless && _format <= Format.BC7_UNorm_SRgb)
		{
			return 16;
		}
		return 0;
	}

	#endregion
}
