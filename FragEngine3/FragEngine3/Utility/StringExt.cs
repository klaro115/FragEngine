namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for working with strings.
/// </summary>
public static class StringExt
{
	#region Methods

	public static bool ReadNextSingle(this string _txt, int _startIdx, out float _outValue, out int _endIdx)
	{
		if (string.IsNullOrEmpty(_txt) || _startIdx >= _txt.Length)
		{
			_outValue = 0;
			_endIdx = 0;
			return false;
		}

		for (int i = _startIdx; i < _txt.Length; i++)
		{
			char c = _txt[i];
			if (char.IsNumber(c))
			{
				_endIdx = _txt.Length;
				for (int j = i + 1; j < _txt.Length; j++)
				{
					if (!char.IsNumber(c))
					{
						_endIdx = j;
						break;
					}
				}
				ReadOnlySpan<char> span = _txt.AsSpan(i, _endIdx - i);
				_outValue = float.Parse(span);
				return true;
			}
		}

		_outValue = 0;
		_endIdx = _txt.Length;
		return false;
	}

	public static bool ReadNextInteger(this string _txt, int _startIdx, out int _outValue, out int _endIdx)
	{
		if (string.IsNullOrEmpty(_txt) || _startIdx >= _txt.Length)
		{
			_outValue = 0;
			_endIdx = 0;
			return false;
		}

		for (int i = _startIdx; i < _txt.Length; i++)
		{
			char c = _txt[i];
			if (char.IsAsciiDigit(c) || c == '-')
			{
				_endIdx = _txt.Length;
				for (int j = i + 1; j < _txt.Length; j++)
				{
					if (!char.IsAsciiDigit(c))
					{
						_endIdx = j;
						break;
					}
				}
				ReadOnlySpan<char> span = _txt.AsSpan(i, _endIdx - i);
				_outValue = int.Parse(span);
				return true;
			}
		}

		_outValue = 0;
		_endIdx = _txt.Length;
		return false;
	}

	/// <summary>
	/// Gets a new string with an incremental index suffixed to it. If the current string does not have a number suffix, a starting
	/// index will be appended. If the current string already ends on a number, the output will have the next higher number.
	/// </summary>
	/// <param name="_txt">This string, which may or may not have a number suffix.</param>
	/// <param name="_startIndexIfMissing">A starting index to start counting on, if the current string does not have a suffix yet.</param>
	/// <returns>A new string with a number suffix added to it.</returns>
	public static string AddIncrementalIndexSuffix(this string _txt, int _startIndexIfMissing = 1)
	{
		if (string.IsNullOrEmpty(_txt)) return string.Empty;

		// Last character is not a number? Append starting index right away:
		if (!char.IsAsciiDigit(_txt[^1]))
		{
			return $"{_txt}{_startIndexIfMissing}";
		}

		// Find start of number suffix:
		int i;
		for (i = _txt.Length - 1; i >= 0; i--)
		{
			char c = _txt[i];
			if (!char.IsAsciiDigit(c))
			{
				break;
			}
		}

		// Parse, then increment number suffix:
		ReadOnlySpan<char> span = _txt.AsSpan(i);
		int currentIndexSuffix = int.Parse(span);
		int newIndexSuffix = currentIndexSuffix + 1;
		
		return $"{_txt}{newIndexSuffix}";
	}

	#endregion
}
