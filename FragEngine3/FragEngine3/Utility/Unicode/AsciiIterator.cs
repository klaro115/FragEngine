using System.Collections;

namespace FragEngine3.Utility.Unicode;

/// <summary>
/// An enumerator for iterating over ASCII encoded byte data while converting it to UTF-16 characters on-the-fly.<para/>
/// NOTE: The ASCII character space can be converted directly to unicode code points, which map directly to corresponding
/// single-char UTF-16 code units. This iterator simply casts each ASCII character (byte) to a uint for unicode codepoint
/// and to char for UTF-16.
/// </summary>
public sealed class AsciiIterator : IEnumerator<char>
{
	#region Types

	public readonly struct Position(int _utf8Position, int _codepointPosition, int _utf16Position)
		{
			public readonly int asciiPosition = _utf8Position;
		public readonly int codepointPosition = _codepointPosition;
		public readonly int utf16Position = _utf16Position;

		public static Position Invalid => new(-1, -1, -1);
		public bool IsValid => asciiPosition >= 0 && codepointPosition >= 0 && utf16Position >= 0;

		public override string ToString() => $"(ASCII: {asciiPosition}, CodePt: {codepointPosition}, UTF-16: {utf16Position})";
	}

	#endregion
	#region Constructors

	public AsciiIterator(IList<byte> _asciiBytes, int _asciiLength)
	{
		asciiBytes = _asciiBytes ?? throw new ArgumentNullException(nameof(_asciiBytes), "ASCII byte list may not be null!");
		asciiLength = Math.Clamp(_asciiLength, 0, asciiBytes.Count);
	}

	~AsciiIterator()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	public readonly IList<byte> asciiBytes;
	public readonly int asciiLength = 0;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the current reading position of ASCII code units (<see cref="byte"/>) in the source array.
	/// </summary>
	public int AsciiPosition { get; private set; } = -1;
	/// <summary>
	/// Gets the current reading position in Unicode codepoints corresponding to the number of ASCII
	/// characters that were read.
	/// </summary>
	public int CodepointPosition { get; private set; } = -1;
	/// <summary>
	/// Gets the current reading position in UTF-16 code units (<see cref="char"/>) corresponding to
	/// the number of Unicode codepoints that were read.
	/// </summary>
	public int Utf16Position { get; private set; } = -1;
	/// <summary>
	/// Gets the current reading positions in one structure containing ASCII byte offset, UTF-16 code units, and in unicode codepoints.
	/// </summary>
	public Position CurrentPosition => new(AsciiPosition, CodepointPosition, Utf16Position);

	public int CurrentCodepoint { get; private set; } = 0;
	public char Current { get; private set; } = '\0';

	object IEnumerator.Current => Current;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _disposing)
	{
		if (_disposing) Reset();
	}

	public bool MoveNext()
	{
		if (AsciiPosition >= asciiLength - 1)
		{
			Current = '\0';
			return false;
		}

		// Convert next ASCII character to unicode codepoint and UTF-16 code units via simple cast:
		AsciiPosition++;
			CodepointPosition++;
			Utf16Position++;

		byte x = asciiBytes[AsciiPosition];

			CurrentCodepoint = x;
			Current = (char)x;

		return Current != '\0';
	}

	public void Reset()
	{
		AsciiPosition = -1;
		CodepointPosition = -1;
		Utf16Position = -1;

		CurrentCodepoint = 0;
		Current = '\0';
	}

	/// <summary>
	/// Advance the enumerator by a given amount of ASCII code units, each of which is equivalent to 1 byte
	/// in the source byte array.
	/// </summary>
	/// <param name="_asciiOffset">The number of ASCII code units (bytes) to advance by.</param>
	/// <returns>The actual number of ASCII characters that was iterated over. This may be less than the given
	/// offset if the end of the source data is reached.</returns>
	public int AdvanceASCII(int _asciiOffset)
	{
		int curOffset;
		int startIdx = AsciiPosition;
		while ((curOffset = AsciiPosition - startIdx) < _asciiOffset && MoveNext()) { }

		return curOffset;
	}

	/// <summary>
	/// Advance the enumerator by a given amount of unicode codepoints, each of which is equivalent to 1
		/// ASCII byte in the source byte array.
	/// </summary>
	/// <param name="_codepointOffset">The number of unicode codepoints to advance by.</param>
	/// <returns>The actual number of unicode codepoints that was iterated over. This may be less than the given
	/// offset if the end of the source data is reached.</returns>
	public int AdvanceCodepoints(int _codepointOffset)
	{
		int curOffset;
		int startIdx = CodepointPosition;
		while ((curOffset = CodepointPosition - startIdx) < _codepointOffset && MoveNext()) { }

		return curOffset;
	}

	/// <summary>
	/// Advance the enumerator by a given amount of UTF-16 code units, each of which is represented by 1
	/// character of type '<see cref="char"/>'.
	/// </summary>
	/// <param name="_utf16Offset"></param>
	/// <returns>The actual number of UTF-16 code units that was iterated over. This may be less than the given
	/// offset if the end of the source data is reached.</returns>
	public int AdvanceUtf16(int _utf16Offset)
	{
		int curOffset = 0;
		while (curOffset < _utf16Offset && MoveNext())
		{
			curOffset++;
		}

		return curOffset;
	}

	/// <summary>
	/// Advance the enumerator until the first occurrance of a given query text is found. If none is found, the
	/// iterator continues until the end of the enumeration.<para/>
	/// NOTE: The enumerator will always advance past the starting position of a search result. It will however
	/// return the starting position of the match. To get the current reading positions after finding a result,
	/// use '<see cref="CurrentPosition"/>'.<para/>
	/// WARNING: The iterator must have been advanced to a valid starting position via a call to '<see cref="MoveNext"/>'
	/// at least once before this search function is used!
	/// </summary>
	/// <param name="_query">A text query to look for in this enumerator's ASCII byte array. If null or empty,
	/// the iterator will ignore the query and stay where it is.</param>
	/// <returns>A structure containing the starting position of the first search result that was encountered.
	/// If no match was found, the position's indices will be all negative, use '<see cref="Position.IsValid"/>'
	/// to check for this. Position indices will be in ASCII byte order, Unicode codepoint count, and UTF-16
	/// code unit count, starting from the beginning of the enumerator's source array.</returns>
	public Position FindNext(string _query)
	{
		if (string.IsNullOrEmpty(_query))
		{
			return Position.Invalid;
		}

		// Iterate over characters until we reach the first symbol of the query string:
		do
		{
			if (Current == _query[0])
			{
				int utf8FoundIdx = AsciiPosition;
				int codepointFoundIdx = CodepointPosition;
				int utf16FoundIdx = Utf16Position;

				// Iterate and check for continued query overlap as we go:
				int i = 1;
				while (MoveNext() && i < _query.Length && Current == _query[i])
				{
					i++;
				}
				if (i >= _query.Length)
				{
					// Return the starting position in all encodings that were involved in the process:
					return new Position(utf8FoundIdx, codepointFoundIdx, utf16FoundIdx);
				}
			}
		}
		while (MoveNext());

		return Position.Invalid;
	}

		public override string ToString()
		{
			return $"ASCII Iterator {CurrentPosition} (ASCII Length: {asciiLength})";
		}

		#endregion
	}

