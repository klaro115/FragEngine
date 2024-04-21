using System.Text;

namespace FragEngine3.Utility;

/// <summary>
/// Helper class containing extension methods for the <see cref="StringBuilder"/> type.
/// </summary>
public static class StringBuilderExt
{
	#region Methods

	/// <summary>
	/// Finds the index of the first occurance of a string within the string builder.
	/// </summary>
	/// <param name="_builder">This string builder.</param>
	/// <param name="_txt">A string we're looking for, may not be null or empty.</param>
	/// <param name="_startIdx">The index from which to start searching for matches.</param>
	/// <returns>The index of the first occurance of the string, or -1, if the string wasn't found.</returns>
	public static int FirstIndexOf(this StringBuilder _builder, string _txt, int _startIdx = 0)
	{
		if (_builder is null || string.IsNullOrEmpty(_txt)) return -1;
		if (_builder.Length < _txt.Length) return -1;

		int maxStartIdx = _builder.Length - _txt.Length;
		char first = _txt[0];

		for (int i = Math.Max(_startIdx, 0); i < maxStartIdx; ++i)
		{
			char c = _builder[i];
			if (c == first)
			{
				for (int j = 1; j < _txt.Length; ++j)
				{
					if (_builder[i + j] != _txt[j])
					{
						goto incompleteMatch;
					}
				}
				return i;
			}
			incompleteMatch: ;
		}
		return -1;
	}

	/// <summary>
	/// Gets the starting and end indices of a line of text within the string builder, from the index of a character within that line.
	/// </summary>
	/// <param name="_builder">This string builder.</param>
	/// <param name="_indexWithinLine">The index of any one character within the line whose start and end we're seeking.</param>
	/// <returns>A range consisting of start and end indices of the text line. Indices are negative if no start or end could be found.</returns>
	public static Range GetLineIndices(this StringBuilder _builder, int _indexWithinLine)
	{
		if (_builder is null) return new(-1, -1);
		if (_indexWithinLine < 0 || _indexWithinLine >= _builder.Length) return new(-1, -1);

		// Find line start:
		char c = '\0';
		int startIdx = _indexWithinLine;
		for (int i = _indexWithinLine; i >= 0; --i)
		{
			c = _builder[i];
			if (c == '\n' ||  c == '\r')
			{
				break;
			}
			startIdx = i;
		}

		// Find line end, include the separator:
		int endIdx = _indexWithinLine;
		for (; endIdx < _builder.Length; ++endIdx)
		{
			c = _builder[endIdx];
			if (c == '\n' || c == '\r')
			{
				break;
			}
		}
		// if line ending is CLRF, included that trailing line feed as well:
		if (c == '\r' &&
			endIdx + 1 < _builder.Length &&
			_builder[endIdx + 1] == '\n')
		{
			endIdx++;
		}

		return new Range(startIdx, endIdx);
	}

	public static Range GetFirstLineIndicesOf(this StringBuilder _builder, string _txt)
	{
		int foundIdx = FirstIndexOf(_builder, _txt);
		if (foundIdx < 0) return new(-1, -1);

		return GetLineIndices(_builder, foundIdx);
	}

	public static StringBuilder Remove(this StringBuilder _builder, Range _removeRange)
	{
		if (_builder is null) return null!;

		int startIdx = _removeRange.Start.Value;
		int length = _removeRange.End.Value - startIdx;

		return _builder.Remove(startIdx, length);
	}

	/// <summary>
	/// Remove all text lines containing a given string from the string builder.
	/// </summary>
	/// <param name="_builder">This string builder.</param>
	/// <param name="_txt">The string we're looking for; all lines containing this text will be removed.</param>
	public static StringBuilder RemoveAllLines(this StringBuilder _builder, string _txt)
	{
		if (_builder is null) return null!;

		int foundIdx = 0;
		while ((foundIdx = FirstIndexOf(_builder, _txt, foundIdx)) > 0)
		{
			Range lineIndices = GetLineIndices(_builder, foundIdx);
			Remove(_builder, lineIndices);
		}

		return _builder;
	}

	#endregion
}
