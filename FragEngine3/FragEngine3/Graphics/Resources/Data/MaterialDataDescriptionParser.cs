using Veldrid;

namespace FragEngine3.Graphics.Resources.Data
{
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

		#endregion
	}
}
