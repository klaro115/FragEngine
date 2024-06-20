using System.Diagnostics.CodeAnalysis;

namespace FragEngine3.Resources;

public readonly struct FourCC : IEquatable<FourCC>
{
	#region Constructors

	public FourCC(uint _packedValue)
	{
		packedValue = _packedValue;
	}
	public FourCC(char _char0, char _char1, char _char2, char _char3)
	{
		packedValue = ((uint)_char3 << 24) | ((uint)_char2 << 16) | ((uint)_char1 << 8) | _char0;
	}
	internal FourCC(string _txt)
	{
		this = TryParseString(_txt, out FourCC fourCC) ? fourCC : invalid;
	}

	#endregion
	#region Fields

	/// <summary>
	/// 32-bit packed integer encoding all four characters.
	/// </summary>
	public readonly uint packedValue;

	public static readonly FourCC invalid = new(0u);

	#endregion
	#region Properties

	public readonly char Character0 => (char)(packedValue & 0xFFu);
	public readonly char Character1 => (char)((packedValue >> 8) & 0xFFu);
	public readonly char Character2 => (char)((packedValue >> 16) & 0xFFu);
	public readonly char Character3 => (char)((packedValue >> 24) & 0xFFu);

	public readonly string AsString => $"{Character0}{Character1}{Character2}{Character3}";

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool result =
			char.IsAsciiLetterOrDigit(Character0) &&
			char.IsAsciiLetterOrDigit(Character1) &&
			char.IsAsciiLetterOrDigit(Character2) &&
			(char.IsAsciiLetterOrDigit(Character3) || Character3 == ' ');
		return result;
	}

	public static bool TryParseString(string _txt, out FourCC _outResult)
	{
		if (string.IsNullOrEmpty(_txt) || _txt.Length != 4)
		{
			_outResult = invalid;
			return false;
		}

		_outResult = new(_txt[0], _txt[1], _txt[2], _txt[3]);
		return _outResult.IsValid();
	}

	public static bool TryParseString(IReadOnlyList<char> _txt, out FourCC _outResult)
	{
		if (_txt is null || _txt.Count != 4 || _txt[0] == '\0')
		{
			_outResult = invalid;
			return false;
		}

		_outResult = new(_txt[0], _txt[1], _txt[2], _txt[3]);
		return _outResult.IsValid();
	}

	public override readonly int GetHashCode() => packedValue.GetHashCode();

	public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is FourCC other && Equals(other);
	public readonly bool Equals(FourCC other) => other.packedValue == packedValue;

	public static bool operator ==(FourCC left, FourCC right) => left.packedValue == right.packedValue;
	public static bool operator !=(FourCC left, FourCC right) => left.packedValue != right.packedValue;

	public override readonly string ToString()
	{
		// Replace non-ASCII, non-readable characters by missing character symbol: (the blank square)
		char c0 = char.IsAsciiLetterOrDigit(Character0) ? Character0 : '\u25A1';
		char c1 = char.IsAsciiLetterOrDigit(Character1) ? Character1 : '\u25A1';
		char c2 = char.IsAsciiLetterOrDigit(Character2) ? Character2 : '\u25A1';
		char c3 = char.IsAsciiLetterOrDigit(Character3) ? Character3 : '\u25A1';

		// Print both human-readable string format, and little-endian packed 32-bit number:
		return $"{c0}{c1}{c2}{c3} (Packed Value: {packedValue:H})";
	}

	#endregion
}
