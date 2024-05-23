using Veldrid;

namespace FragEngine3.Graphics.Resources.Data;

public static class MaterialDataDescriptionParser
{
	#region Fields

	private static readonly char[] addressModeChars =
	[
		'W',    // Wrap
		'M',    // Mirror
		'C',    // Clamp
		'B',    // Border color
	];
	private static readonly char[] comparisonKindChars =
	[
		'N',    // Never
		'<',    // Less than
		'=',    // Equal
		'L',    // Less or equal
		'>',    // Greater than
		'!',    // Not equal
		'G',    // Greater or equal,
		'A',    // Allways
	];
	private static readonly char[] borderColorChars =
	[
		'T',    // Transparent black (0, 0, 0, 0)
		'B',    // Black
		'W',    // White
	];

	#endregion
	#region Methods

	public static string CreateDescription_Sampler(SamplerDescription _desc)
	{
		// Format: "WWW_7_>_B"

		char addressModeU = addressModeChars[(int)_desc.AddressModeU];
		char addressModeV = addressModeChars[(int)_desc.AddressModeV];
		char addressModeW = addressModeChars[(int)_desc.AddressModeW];

		char filter = (char)('0' + (int)_desc.Filter);

		char comparisonKind = _desc.ComparisonKind != null
			? comparisonKindChars[(int)_desc.ComparisonKind]
			: 'N';

		char borderColor = borderColorChars[(int)_desc.BorderColor];

		return $"{addressModeU}{addressModeV}{addressModeW}_{filter}_{comparisonKind}_{borderColor}";
	}

	public static SamplerDescription DecodeDescription_Sampler(string? _description)
	{
		if (string.IsNullOrEmpty(_description) || _description.Length < 9)
		{
			return SamplerDescription.Point;
		}

		return new SamplerDescription(
			(SamplerAddressMode)GetCharIndex(_description[0], addressModeChars),
			(SamplerAddressMode)GetCharIndex(_description[1], addressModeChars),
			(SamplerAddressMode)GetCharIndex(_description[2], addressModeChars),
			(SamplerFilter)(_description[4] - '0'),
			(ComparisonKind)GetCharIndex(_description[6], comparisonKindChars),
			4,
			ushort.MaxValue,
			0,
			0,
			(SamplerBorderColor)GetCharIndex(_description[8], comparisonKindChars));


		static int GetCharIndex(char _c, char[] _array)
		{
			for (int i = 0; i < _array.Length; ++i)
			{
				if (_c == _array[i]) return i;
			}
			return 0;
		}
	}

	public static ulong CreateIdentifier_Sampler(SamplerDescription _desc)
	{
		// First 32 bits:
		uint addressModeU = (uint)_desc.AddressModeU << 29;				//3 bits
		uint addressModeV = (uint)_desc.AddressModeU << 26;				//3 bits
		uint addressModeW = (uint)_desc.AddressModeU << 23;				//3 bits

		uint filter = (uint)_desc.Filter << 19;							// 4 bits

		uint comparisonKind = (uint)(_desc.ComparisonKind ?? 0) << 16;	// 3 bits
		uint maxAnisotropy = _desc.MaximumAnisotropy << 12;				// 4 bits

		uint borderColor = (uint)_desc.BorderColor << 10;               // 2 bits
		uint lodBias;
		unchecked { lodBias = (uint)((_desc.LodBias >> 21) & 0x3FF); }	// 10 bits (Note: Range remapped to 10 bits. This might be too aggressive and lack fine-grained control)

		uint first = addressModeU | addressModeV | addressModeW | filter | comparisonKind | maxAnisotropy | borderColor | lodBias;

		// Second 32 bits:
		uint minLod = _desc.MinimumLod & 0xFFFF0000u;
		uint maxLod = (_desc.MaximumLod >> 16) & 0x0000FFFFu;

		uint second = minLod | maxLod;

		return ((ulong)first << 32) | second;
	}

	public static SamplerDescription DecodeIdentifier_Sampler(ulong _id)
	{
		uint first = (uint)(_id >> 32);
		uint second = (uint)(_id & 0xFFFFFFFFu);

		int lodBias;
		unchecked { lodBias = (int)((first & 0x3FF) << 21); }

		return new SamplerDescription(
			(SamplerAddressMode)((first >> 29) & 0x07),
			(SamplerAddressMode)((first >> 26) & 0x07),
			(SamplerAddressMode)((first >> 23) & 0x07),

			(SamplerFilter)((first >> 19) & 0x0F),

			(ComparisonKind)((first >> 16) & 0x07),
			(first >> 12) & 0x0F,

			(second & 0xFFFF0000u),
			(second & 0x0000FFFFu) << 16,

			lodBias,
			(SamplerBorderColor)((first >> 10) & 0x03));
	}

	#endregion
}
