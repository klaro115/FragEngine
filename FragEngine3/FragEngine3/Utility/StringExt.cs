namespace FragEngine3.Utility;

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

	#endregion
}
