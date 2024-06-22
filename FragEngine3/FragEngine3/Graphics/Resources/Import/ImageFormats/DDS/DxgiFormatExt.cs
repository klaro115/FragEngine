using System.Text;
using Veldrid;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public static class DxgiFormatExt
{
	#region Fields

	private static readonly Dictionary<Format, PixelFormat> formatMap = new()
	{
		// Mapped automagically by name:
		[Format.R32G32B32A32_Float] = PixelFormat.R32_G32_B32_A32_Float,
		[Format.R32G32B32A32_UInt] = PixelFormat.R32_G32_B32_A32_UInt,
		[Format.R32G32B32A32_SInt] = PixelFormat.R32_G32_B32_A32_SInt,
		[Format.R16G16B16A16_Float] = PixelFormat.R16_G16_B16_A16_Float,
		[Format.R16G16B16A16_UNorm] = PixelFormat.R16_G16_B16_A16_UNorm,
		[Format.R16G16B16A16_UInt] = PixelFormat.R16_G16_B16_A16_UInt,
		[Format.R16G16B16A16_SNorm] = PixelFormat.R16_G16_B16_A16_SNorm,
		[Format.R16G16B16A16_SInt] = PixelFormat.R16_G16_B16_A16_SInt,
		[Format.R32G32_Float] = PixelFormat.R32_G32_Float,
		[Format.R32G32_UInt] = PixelFormat.R32_G32_UInt,
		[Format.R32G32_SInt] = PixelFormat.R32_G32_SInt,
		[Format.R10G10B10A2_UNorm] = PixelFormat.R10_G10_B10_A2_UNorm,
		[Format.R10G10B10A2_UInt] = PixelFormat.R10_G10_B10_A2_UInt,
		[Format.R11G11B10_Float] = PixelFormat.R11_G11_B10_Float,
		[Format.R8G8B8A8_UNorm] = PixelFormat.R8_G8_B8_A8_UNorm,
		[Format.R8G8B8A8_UNorm_SRgb] = PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
		[Format.R8G8B8A8_UInt] = PixelFormat.R8_G8_B8_A8_UInt,
		[Format.R8G8B8A8_SNorm] = PixelFormat.R8_G8_B8_A8_SNorm,
		[Format.R8G8B8A8_SInt] = PixelFormat.R8_G8_B8_A8_SInt,
		[Format.R16G16_Float] = PixelFormat.R16_G16_Float,
		[Format.R16G16_UNorm] = PixelFormat.R16_G16_UNorm,
		[Format.R16G16_UInt] = PixelFormat.R16_G16_UInt,
		[Format.R16G16_SNorm] = PixelFormat.R16_G16_SNorm,
		[Format.R16G16_SInt] = PixelFormat.R16_G16_SInt,
		[Format.R32_Float] = PixelFormat.R32_Float,
		[Format.R32_UInt] = PixelFormat.R32_UInt,
		[Format.R32_SInt] = PixelFormat.R32_SInt,
		[Format.R8G8_UNorm] = PixelFormat.R8_G8_UNorm,
		[Format.R8G8_UInt] = PixelFormat.R8_G8_UInt,
		[Format.R8G8_SNorm] = PixelFormat.R8_G8_SNorm,
		[Format.R8G8_SInt] = PixelFormat.R8_G8_SInt,
		[Format.R16_Float] = PixelFormat.R16_Float,
		[Format.R16_UNorm] = PixelFormat.R16_UNorm,
		[Format.R16_UInt] = PixelFormat.R16_UInt,
		[Format.R16_SNorm] = PixelFormat.R16_SNorm,
		[Format.R16_SInt] = PixelFormat.R16_SInt,
		[Format.R8_UNorm] = PixelFormat.R8_UNorm,
		[Format.R8_UInt] = PixelFormat.R8_UInt,
		[Format.R8_SNorm] = PixelFormat.R8_SNorm,
		[Format.R8_SInt] = PixelFormat.R8_SInt,
		[Format.BC2_UNorm] = PixelFormat.BC2_UNorm,
		[Format.BC2_UNorm_SRgb] = PixelFormat.BC2_UNorm_SRgb,
		[Format.BC3_UNorm] = PixelFormat.BC3_UNorm,
		[Format.BC3_UNorm_SRgb] = PixelFormat.BC3_UNorm_SRgb,
		[Format.BC4_UNorm] = PixelFormat.BC4_UNorm,
		[Format.BC4_SNorm] = PixelFormat.BC4_SNorm,
		[Format.BC5_UNorm] = PixelFormat.BC5_UNorm,
		[Format.BC5_SNorm] = PixelFormat.BC5_SNorm,
		[Format.B8G8R8A8_UNorm] = PixelFormat.B8_G8_R8_A8_UNorm,
		[Format.B8G8R8A8_UNorm_SRgb] = PixelFormat.B8_G8_R8_A8_UNorm_SRgb,
		[Format.BC7_UNorm] = PixelFormat.BC7_UNorm,
		[Format.BC7_UNorm_SRgb] = PixelFormat.BC7_UNorm_SRgb,

		// Manually mapped:
		[Format.D32_Float_S8X24_UInt] = PixelFormat.D32_Float_S8_UInt,
		[Format.D24_UNorm_S8_UInt] = PixelFormat.D24_UNorm_S8_UInt,
		[Format.A8_UNorm] = PixelFormat.R8_UNorm,
		[Format.R8G8_B8G8_UNorm] = PixelFormat.R8_G8_B8_A8_UNorm,   // non-ideal mapping.
		[Format.BC1_UNorm] = PixelFormat.BC1_Rgba_UNorm,			// optimistic RGBA mapping, could also be RGB.
		[Format.BC1_UNorm_SRgb] = PixelFormat.BC1_Rgba_UNorm_SRgb,  // // optimistic RGBA mapping, could also be RGB.
	};

	#endregion
	#region Methods

	//TEST: Automatic mapping function for DXGI surface formats and Veldrid pixel formats.
	public static void MapFormats()
	{
		Format[] dxgiFormats = Enum.GetValues<Format>();
		PixelFormat[] pixelFormats = Enum.GetValues<PixelFormat>();

		List<Format> unmatchesDxgiFormats = [];
		StringBuilder builder = new(1024);

		List<string> parts = new(6);
		foreach (Format dxgiFormat in dxgiFormats)
		{
			string dxgiName = dxgiFormat.ToString();

			bool prevWasNumber = false;
			char prevC = '1';
			int partStartIdx = 0;
			for (int i = 0; i < dxgiName.Length; ++i)
			{
				char c = dxgiName[i];
				bool curIsNumber = char.IsAsciiDigit(c);
				if (!curIsNumber && prevWasNumber)
				{
					int partEndIdx = i;
					if (prevC == '_')
					{
						partEndIdx--;
					}

					string part = dxgiName.Substring(partStartIdx, partEndIdx - partStartIdx);
					parts.Add(part);
					partStartIdx = i;
				}
				prevWasNumber = curIsNumber;
				prevC = c;
			}
			if (partStartIdx < dxgiName.Length)
			{
				if (dxgiName[partStartIdx] == '_')
				{
					partStartIdx++;
				}

				string part = dxgiName.Substring(partStartIdx, dxgiName.Length - partStartIdx);
				parts.Add(part);
			}

			string pixelFormatName = string.Join('_', parts);
			parts.Clear();

			var matches = from p in pixelFormats
						  where p.ToString() == pixelFormatName
						  select p;
			if (!matches.Any())
			{
				unmatchesDxgiFormats.Add(dxgiFormat);
				continue;
			}

			if (matches.Count() > 1)
			{
				// Multiple matches:
				foreach (PixelFormat pixelFormat in matches)
				{
					builder.Append("\t\t//[Format.").Append(dxgiFormat).Append("] = PixelFormat.").Append(pixelFormat).AppendLine(",");
				}
			}
			else
			{
				// Singular matches:
				builder.Append("\t\t[Format.").Append(dxgiFormat).Append("] = PixelFormat.").Append(matches.First()).AppendLine(",");
			}
		}

		Console.WriteLine("\n\n\nBEGIN: DXGI/Pixel format matches:\n");
		Console.WriteLine(builder);
		Console.WriteLine("\nEND.\n\n\n");

		Console.WriteLine("BEGIN: Unmatched DXGI formats:\n");
		foreach (Format dxgiFormat in unmatchesDxgiFormats)
		{
			Console.Write("\t\t");
			Console.Write(dxgiFormat);
			Console.WriteLine(',');
		}
		Console.WriteLine("\nEND.\n\n\n");
	}

	public static bool TryGetPixelFormat(this Format _dxgiFormat, out PixelFormat _outPixelFormat)
	{
		bool result = formatMap.TryGetValue(_dxgiFormat, out _outPixelFormat);
		if (!result)
		{
			_outPixelFormat = PixelFormat.R8_UNorm;
		}
		return result;
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

	public static uint GetCompressionBlockSize(this PixelFormat _format)
	{
		switch (_format)
		{
			// BC1 (DXT1)
			case PixelFormat.BC1_Rgb_UNorm:
			case PixelFormat.BC1_Rgb_UNorm_SRgb:
			case PixelFormat.BC1_Rgba_UNorm:
			case PixelFormat.BC1_Rgba_UNorm_SRgb:
				return 64;

			// BC2 (DXT2 & DXT3)
			case PixelFormat.BC2_UNorm:
			case PixelFormat.BC2_UNorm_SRgb:
				return 128;

			// BC3 (DXT4 & DXT5)
			case PixelFormat.BC3_UNorm_SRgb:
				return 128;

			// BC4:
			case PixelFormat.BC4_UNorm:
			case PixelFormat.BC4_SNorm:
				return 48;

			// BC5:
			case PixelFormat.BC5_UNorm:
			case PixelFormat.BC5_SNorm:
				return 128;

			// BC7:
			case PixelFormat.BC7_UNorm:
			case PixelFormat.BC7_UNorm_SRgb:
				return 128;

			// ETC2:
			case PixelFormat.ETC2_R8_G8_B8_UNorm:
			case PixelFormat.ETC2_R8_G8_B8_A1_UNorm:
			case PixelFormat.ETC2_R8_G8_B8_A8_UNorm:
				return 64;

			default:
				return 0;
		}
	}

	#endregion
}
