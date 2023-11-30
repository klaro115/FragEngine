using System.Collections;

namespace FragEngine3.Utility.Unicode
{
	/// <summary>
	/// An enumerator for iterating over UTF-8 encoded byte data while converting it to UTF-16 characters on-the-fly.<para/>
	/// NOTE: For each call to '<see cref="MoveNext"/>', 1-4 UTF-8 code units are read until a full unicode codepoint can be
	/// parsed, which is then converted to UTF-16. Up to 2 UTF-16 code units (aka, characters) may be generated for each
	/// codepoint, the high surrogate is returned right away, while the low surrogate of which is returned upon the next call
	/// to '<see cref="MoveNext"/>'.
	/// </summary>
	public sealed class Utf16Iterator : IEnumerator<char>
	{
		#region Types

		public readonly struct Position
		{
			public Position(int _utf8Position, int _codepointPosition, int _utf16Position)
			{
				utf8Position = _utf8Position;
				codepointPosition = _codepointPosition;
				utf16Position = _utf16Position;
			}

			public readonly int utf8Position;
			public readonly int codepointPosition;
			public readonly int utf16Position;

			public static Position Invalid => new(-1, -1, -1);
			public bool IsValid => utf8Position >= 0 && codepointPosition >= 0 && utf16Position >= 0;

			public override string ToString() => $"(UTF-8: {utf8Position}, CodePt: {codepointPosition}, UTF-16: {utf16Position})";
		}

		#endregion
		#region Constructors

		public Utf16Iterator(IList<byte> _utf8Bytes, int _utf8Length)
		{
			utf8Bytes = _utf8Bytes ?? throw new ArgumentNullException(nameof(_utf8Bytes), "UTF-8 byte list may not be null!");
			utf8Length = Math.Clamp(_utf8Length, 0, utf8Bytes.Count);
		}

		~Utf16Iterator()
		{
			Dispose(false);
		}

		#endregion
		#region Fields

		public readonly IList<byte> utf8Bytes;
		public readonly int utf8Length = 0;

		private char lowSurrogate = '\0';	//pending low surrogate.

		#endregion
		#region Properties

		/// <summary>
		/// Gets the current reading position of UTF-8 code units (<see cref="byte"/>) in the source array.
		/// </summary>
		public int Utf8Position { get; private set; } = -1;
		/// <summary>
		/// Gets the current reading position in Unicode codepoints corresponding to the number of UTF-8
		/// characters that were read.
		/// </summary>
		public int CodepointPosition { get; private set; } = -1;
		/// <summary>
		/// Gets the current reading position in UTF-16 code units (<see cref="char"/>) corresponding to
		/// the number of Unicode codepoints that were read.
		/// </summary>
		public int Utf16Position { get; private set; } = -1;
		/// <summary>
		/// Gets the current reading positions in one structure containing UTF-8 byte offset, UTF-16 code units, and in unicode codepoints.
		/// </summary>
		public Position CurrentPosition => new(Utf8Position, CodepointPosition, Utf16Position);

		public int CurrentCodepoint { get; private set; } = 0;
		public char Current { get; private set; } = '\0';

		object IEnumerator.Current => Current;

		/// <summary>
		/// Gets whether a low surrogate char is still pending that should stay paired with a preceding high
		/// surrogate code unit which we moved to just now.<para/>
		/// HINT: If this is true after an iteration process has concluded, you might want to call <see cref="MoveNext"/>
		/// one more time, to ensure the next loop doesn't start on an incomplete character's low surrogate.
		/// </summary>
		public bool IsLowSurrogatePending => lowSurrogate != '\0';

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
			if (Utf8Position >= utf8Length - 1)
			{
				Current = '\0';
				return false;
			}

			// If there was a low surrogate as part of the current codepoint, return that first:
			if (IsLowSurrogatePending)
			{
				Current = lowSurrogate;
				lowSurrogate = '\0';

				Utf16Position++;
				return true;
			}

			// Convert next UTF-8 character to unicode codepoint, then return UTF-16 code units:
			Utf8Position++;

			byte x = utf8Bytes[Utf8Position];

			// Single-byte:
			if ((x & 0x80) == 0)	// 0XXX XXXX
			{
				CurrentCodepoint = x;
				Current = (char)x;

				CodepointPosition++;
				Utf16Position++;
			}
			// 2 bytes:				// 110X XXXX
			else if ((x & 0xE0) == 0xC0 && Utf8Position + 1 < utf8Length)
			{
				byte y = utf8Bytes[Utf8Position + 1];

				CurrentCodepoint = ((x & 0x1F) << 6) | (y & 0x3F);
				int utf16Size = ConvertCodepointToUtf16();

				Utf8Position += 1;
				CodepointPosition++;
				Utf16Position += utf16Size;
			}
			// 3 bytes:				// 1110 XXXX
			else if ((x & 0xF0) == 0xE0 && Utf8Position + 2 < utf8Length)
			{
				byte y = utf8Bytes[Utf8Position + 1];
				byte z = utf8Bytes[Utf8Position + 2];

				CurrentCodepoint = ((x & 0x0F) << 12) | ((y & 0x3F) << 6) | (z & 0x3F);
				int utf16Size = ConvertCodepointToUtf16();

				Utf8Position += 2;
				CodepointPosition++;
				Utf16Position += utf16Size;
			}
			// 4 bytes:				// 1111 0XXX
			else if ((x & 0xF8) == 0xF0 && Utf8Position + 3 < utf8Length)
			{
				byte y = utf8Bytes[Utf8Position + 1];
				byte z = utf8Bytes[Utf8Position + 2];
				byte w = utf8Bytes[Utf8Position + 3];

				CurrentCodepoint = ((x & 0x07) << 18) | ((y & 0x3F) << 12) | ((z & 0x3F) << 6) | (w & 0x3F);
				int utf16Size = ConvertCodepointToUtf16();

				Utf8Position += 3;
				CodepointPosition++;
				Utf16Position += utf16Size;
			}
			// Invalid character, should never occur:
			else
			{
				CurrentCodepoint = '?';
				Current = '?';
				lowSurrogate = '\0';

				CodepointPosition++;
				Utf16Position ++;
			}

			return Current != '\0';
		}

		private int ConvertCodepointToUtf16()
		{
			if (CurrentCodepoint < 0x10000)
			{
				Current = (char)CurrentCodepoint;
				lowSurrogate = '\0';

				return 1;
			}
			else
			{
				int x = CurrentCodepoint - 0x10000;
				int high = (x >> 10) & 0x3FF;
				int low = x & 0x3FF;

				Current = (char)(0xD800 | high);
				lowSurrogate = (char)(0xDC00 | low);

				return 2;
			}
		}

		public void Reset()
		{
			Utf8Position = -1;
			CodepointPosition = -1;
			Utf16Position = -1;

			CurrentCodepoint = 0;
			Current = '\0';
			lowSurrogate = '\0';
		}

		/// <summary>
		/// Advance the enumerator by a given amount of UTF-8 code units, each of which is equivalent to 1 byte
		/// in the source byte array.
		/// </summary>
		/// <param name="_utf8Offset">The number of UTF-8 code units (bytes) to advance by.</param>
		/// <returns>The actual number of UTF-8 characters that was iterated over. This may be less than the given
		/// offset if the end of the source data is reached, or larger by 1-3 bytes, if the last character was made
		/// up of multiple UTF-8 code units which need to be read fully before the codepoint can be parsed.</returns>
		public int AdvanceUtf8(int _utf8Offset)
		{
			int curOffset;
			int startIdx = Utf8Position;
			while ((curOffset = Utf8Position - startIdx) < _utf8Offset && MoveNext()) { }

			return curOffset;
		}

		/// <summary>
		/// Advance the enumerator by a given amount of unicode codepoints, each of which is equivalent to 1-4
		/// UTF-8 bytes in the source byte array.
		/// </summary>
		/// <param name="_codepointOffset">The number of unicode codepoints to advance by.</param>
		/// <param name="_finishTrailingSurrogatePair">Whether to advance by up to 1 additional UTF-16 code unit,
		/// if the last UTF-16 character was actually part of a surrogate pair. This is useful to ensure processing
		/// using this enumeration doesn't suddenly resume on an incomplete character.</param>
		/// <returns>The actual number of unicode codepoints that was iterated over. This may be less than the given
		/// offset if the end of the source data is reached.</returns>
		public int AdvanceCodepoints(int _codepointOffset, bool _finishTrailingSurrogatePair = true)
		{
			int curOffset;
			int startIdx = CodepointPosition;
			while ((curOffset = CodepointPosition - startIdx) < _codepointOffset && MoveNext()) { }

			if (_finishTrailingSurrogatePair && IsLowSurrogatePending)
			{
				MoveNext();
			}
			return curOffset;
		}

		/// <summary>
		/// Advance the enumerator by a given amount of UTF-16 code units, each of which is represented by 1
		/// character of type '<see cref="char"/>'.
		/// </summary>
		/// <param name="_utf16Offset"></param>
		/// <param name="_finishTrailingSurrogatePair">Whether to advance by up to 1 additional code unit, if the
		/// last UTF-16 character was actually part of a surrogate pair. This is useful to ensure processing using
		/// this enumeration doesn't suddenly resume on an incomplete character.</param>
		/// <returns>The actual number of UTF-16 code units that was iterated over. This may be less than the given
		/// offset if the end of the source data is reached, or larger by 1 char, if the last character was made up
		/// of 2 UTF-16 code units (high and low surrogates). The latter can only happen if the parameter
		/// '_finishTrailingSurrogatePair' is set to true.</returns>
		public int AdvanceUtf16(int _utf16Offset, bool _finishTrailingSurrogatePair = true)
		{
			int curOffset = 0;
			while (curOffset < _utf16Offset && MoveNext())
			{
				curOffset++;
			}

			if (_finishTrailingSurrogatePair && IsLowSurrogatePending && MoveNext())
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
		/// <param name="_query">A text query to look for in this enumerator's UTF-8 byte array. If null or empty,
		/// the iterator will ignore the query and stay where it is.</param>
		/// <returns>A structure containing the starting position of the first search result that was encountered.
		/// If no match was found, the position's indices will be all negative, use '<see cref="Position.IsValid"/>'
		/// to check for this. Position indices will be in UTF-8 byte order, Unicode codepoint count, and UTF-16
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
					int utf8FoundIdx = Utf8Position;
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

		#endregion
	}
}

