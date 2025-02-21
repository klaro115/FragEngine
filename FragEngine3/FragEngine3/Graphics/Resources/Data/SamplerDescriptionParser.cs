using Veldrid;

namespace FragEngine3.Graphics.Resources.Data;

/// <summary>
/// Helper class for creating IDs and descriptions of <see cref="Sampler"/> state objects.
/// </summary>
public static class SamplerDescriptionParser
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
		'A',    // Always
	];
	private static readonly char[] borderColorChars =
	[
		'T',    // Transparent black (0, 0, 0, 0)
		'B',    // Black
		'W',    // White
	];

	#endregion
	#region Methods

	/// <summary>
	/// Creates a descriptive fixed-length string that encodes all settings of a <see cref="Sampler"/>.
	/// This string is intended as a human-readable serialized form for sampler states.
	/// </summary>
	/// <param name="_desc">A sampler description which we want to serialize to a descriptive string.</param>
	/// <returns>A string representation of the sampler description.</returns>
	public static string CreateDescriptionText(this SamplerDescription _desc)
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

	/// <summary>
	/// Parses a descriptive fixed-length string into a description of a <see cref="Sampler"/> state.
	/// </summary>
	/// <param name="_description">The descriptive string we wish to parse.</param>
	/// <returns>The parsed sampler state description. On failure, <see cref="SamplerDescription.Point"/> is
	/// returned as a fallback.</returns>
	public static SamplerDescription DecodeDescriptionText(string? _description)
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

	/// <summary>
	/// Encodes all settings of a sampler description into a descriptive ID number for faster lookup.
	/// This ID is used as instead of a hash for fast lookup and comparison of samplers.
	/// </summary>
	/// <param name="_desc">A sampler description which we want to serialize to a descriptive ID.</param>
	/// <returns>A 64-bit unsigned integer that serves as a descriptive ID.</returns>
	public static ulong CreateIdentifier(this SamplerDescription _desc)
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

	/// <summary>
	/// Parses a sampler description from a descriptive ID number.
	/// </summary>
	/// <param name="_id">A 64-bit unsigned integer ID whose value encodes a description of a sampler.</param>
	/// <returns>The parsed sampler description.</returns>
	public static SamplerDescription DecodeIdentifier(ulong _id)
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
