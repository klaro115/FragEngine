using FragEngine3.Utility.Unicode;

namespace FragEngine3.Utility
{
	[Obsolete($"Deprecated and to be replaced by specialized text iterators, such as the {nameof(Utf16Iterator)}.")]
	public static class CharEnumerationTools
	{

		//TODO: Replace this by dedicated Iterator types for UTF-8, UTF-16 and unicode codepoints! Indexing and usability are a mess right now.

		#region Types

		public sealed class CharIteratorState
		{
			public int utf8Position = 0;
			public int utf16Position = 0;
			public int codepointPosition = 0;

			public static CharIteratorState Zero => new();

			public void Reset()
			{
				utf8Position = 0;
				utf16Position = 0;
				codepointPosition = 0;
			}
		}

		#endregion
		#region Methods

		/// <summary>
		/// Get an enumerator for iterating over a UTF-8 encoded byte array as UTF-16 characters.
		/// </summary>
		/// <param name="_utf8Bytes">A byte array containing UTF-8 encoded text, must be non-null.</param>
		/// <param name="_state">A state object for tracking the reading offset in UTF-8, UTF-16, and unicode codepoints.
		/// Create a new state object or use '<see cref="CharIteratorState.Zero"/>' if you do not need this info.</param>
		/// <param name="_utf8ByteLength">The number of bytes encoding the text within the byte array, must be non-negative
		/// and may not be greater than the array's length.</param>
		/// <returns>An enumerator yielding <see cref="char"/> type UTF-16 characters read from UTF-8 bytes.</returns>
		public static IEnumerator<char> ReadUtf16FromUtf8Bytes(byte[] _utf8Bytes, CharIteratorState _state, int _utf8ByteLength)
		{
			// If null or no bytes to read, return only a string terminator:
			if (_utf8Bytes == null || _utf8ByteLength == 0)
			{
				yield return '\0';
				yield break;
			}

			_state ??= CharIteratorState.Zero;

			int codePoint = -1;
			IEnumerator<int> e = ReadCodepointsFromUtf8Bytes(_utf8Bytes, _state, _utf8ByteLength);
			while (e.MoveNext() && (codePoint = e.Current) != 0)
			{
				// Single-char UTF-16 character:
				if (codePoint < 0xFFFF)
				{
					_state.utf16Position += 1;
					yield return (char)codePoint;
				}
				// Double-char UTF-16 character:
				else
				{
					_state.utf16Position += 2;
					codePoint -= 0x10000;
					yield return (char)(0xD800 | codePoint);
					yield return (char)(0xDC00 | (codePoint & 0x3FF));
				}
			}

			if (codePoint != 0)
			{
				yield return '\0';
			}
		}

		/// <summary>
		/// Get an enumerator for iterating over a UTF-8 encoded byte array as unicode codepoints.
		/// </summary>
		/// <param name="_utf8Bytes">A byte array containing UTF-8 encoded text, must be non-null.</param>
		/// <param name="_state">A state object for tracking the reading offset in UTF-8 and unicode codepoints.
		/// Create a new state object or use '<see cref="CharIteratorState.Zero"/>' if you do not need this info.</param>
		/// <param name="_utf8ByteLength">The number of bytes encoding the text within the byte array, must be non-negative
		/// and may not be greater than the array's length.</param>
		/// <returns>An enumerator yielding <see cref="int"/> type unicode codepoints read from UTF-8 bytes.</returns>
		public static IEnumerator<int> ReadCodepointsFromUtf8Bytes(byte[] _utf8Bytes, CharIteratorState _state, int _utf8ByteLength)
		{
			// If null or no bytes to read, return only a string terminator:
			if (_utf8Bytes == null || _utf8ByteLength == 0)
			{
				yield return 0;
				yield break;
			}

			// Ensure length is clamped to the given array's length:
			if (_utf8ByteLength > _utf8Bytes.Length) _utf8ByteLength = _utf8Bytes.Length;

			_state ??= CharIteratorState.Zero;

			int codePoint = 0;
			for (; _state.utf8Position < _utf8ByteLength; ++_state.utf8Position)
			{
				byte x = _utf8Bytes[_state.utf8Position];

				// Single-byte UTF-8/ASCII character:
				if ((x & 0x80) == 0)
				{
					_state.utf8Position += 1;
					_state.codepointPosition ++;

					yield return codePoint = x;
				}
				// Multi-byte UTF-8 characters:
				else
				{
					// 2 bytes:
					if ((x & 0xE0) == 0xC0)
					{
						byte y = _utf8Bytes[_state.utf8Position + 1];

						_state.utf8Position += 1;
						_state.codepointPosition++;

						yield return codePoint = ((x & 0x1F) << 6) | (y & 0x3F);
					}
					// 3 bytes:
					else if ((x & 0xF0) == 0xE0)
					{
						byte y = _utf8Bytes[_state.utf8Position + 1];
						byte z = _utf8Bytes[_state.utf8Position + 2];

						_state.utf8Position += 2;
						_state.codepointPosition++;

						yield return codePoint = ((x & 0x0F) << 12) | ((y & 0x3F) << 6) | (z & 0x3F);
					}
					// 4 bytes:
					else if ((x & 0xF8) == 0xF0)
					{
						byte y = _utf8Bytes[_state.utf8Position + 1];
						byte z = _utf8Bytes[_state.utf8Position + 2];
						byte w = _utf8Bytes[_state.utf8Position + 3];

						_state.utf8Position += 3;
						_state.codepointPosition++;

						yield return codePoint = ((x & 0x07) << 18) | ((y & 0x3F) << 12) | ((z & 0x3F) << 6) | (w & 0x3F);
					}
				}
			}

			// Append null terminator if the string didn't end in one:
			if (codePoint != 0)
			{
				_state.codepointPosition++;
				yield return 0;
			}
		}

		public static IEnumerator<int> ReadCodepointsFromUtf16Enumerator(IEnumerator<char> _utf16Enumerator)
		{
			// If null or no code units to read, return only a string terminator:
			if (_utf16Enumerator == null)
			{
				yield return 0;
				yield break;
			}

			char prevC = '\0';
			char curC = (char)0xFFFF;
			while (_utf16Enumerator.MoveNext() && (curC = _utf16Enumerator.Current) != '\0')
			{
				// Low surrogate:
				if ((curC & 0xDC) == 0xDC)
				{
					int high = (prevC - 0xD8) << 10;
					int low = curC - 0xDC;
					yield return high + low;
				}
				// High surrogate:
				else if ((curC & 0xDC) == 0xD8)
				{
					//skip and wait for low surrogate.
				}
				// BMP, single code unit:
				else
				{
					yield return curC;
				}
				prevC = curC;
			}

			if (curC != '\0')
			{
				yield return 0;
			}
		}

		/// <summary>
		/// Advance a UTF-16 enumerator by a given number of characters.
		/// </summary>
		/// <param name="_utf16Enumerator">An enumerator for going over UTF-16 characters.</param>
		/// <param name="_utf16Offset">The number of UTF-16 code units (chars) to advance the iterator by.</param>
		/// <returns>The actual number of characters that was iterated over. May be less than the given offset if
		/// a string terminator was hit or the enumeration ended.</returns>
		public static int AdvanceUtf16EnumeratorByCharacters(IEnumerator<char> _utf16Enumerator, int _utf16Offset)
		{
			if (_utf16Enumerator == null || _utf16Offset <= 0)
			{
				return 0;
			}

			int curOffset = 0;
			while (curOffset < _utf16Offset && _utf16Enumerator.MoveNext() && _utf16Enumerator.Current != '\0')
			{
				curOffset++;
			}

			return curOffset;
		}

		/// <summary>
		/// Advance a UTF-16 enumerator by a given number of characters.
		/// </summary>
		/// <param name="_utf16Enumerator">An enumerator for going over UTF-16 characters.</param>
		/// <param name="_codepointOffset">The number of unicode codepoints to advance the iterator by.</param>
		/// <returns>The actual number of codepoints that was iterated over. May be less than the given offset if
		/// a string terminator was hit or the enumeration ended.</returns>
		public static int AdvanceUtf16EnumeratorByCodepoints(IEnumerator<char> _utf16Enumerator, int _codepointOffset)
		{
			if (_utf16Enumerator == null || _codepointOffset <= 0)
			{
				return 0;
			}

			int curOffset = 0;
			char c;
			while (curOffset < _codepointOffset && _utf16Enumerator.MoveNext() && (c = _utf16Enumerator.Current) != '\0')
			{
				// Only increment offset if the character is not a low surrogate code unit:
				if ((c & 0xDC) != 0xDC)
				{
					curOffset++;
				}
			}

			return curOffset;
		}

		/// <summary>
		/// Advance a UTF-16 enumerator by a given number of equivalent UTF-8 code units.
		/// </summary>
		/// <param name="_utf16Enumerator">An enumerator for going over UTF-16 characters.</param>
		/// <param name="_state">An iterator state tracking the iteration over the UTF-16 enumerator. May not be null,
		/// and must be tied to the given iterator; endless loop ensues if an unrelated state object is used.</param>
		/// <param name="_utf8Offset">The number of UTF-8 code units (bytes) to advance the iterator by.</param>
		/// <returns>The actual number of UTF-8 bytes that was iterated over. May be less than the given offset if
		/// a string terminator was hit or the enumeration ended.</returns>
		public static int AdvanceUtf16EnumeratorByUtf8Bytes(IEnumerator<char> _utf16Enumerator, CharIteratorState _state, int _utf8Offset)
		{
			if (_utf16Enumerator == null || _utf8Offset <= 0 || _state == null)
			{
				return 0;
			}

			// Use UTF-8 position from state object to measure current offset as we go:
			int startPosition = _state.utf8Position;
			while (_state.utf8Position - startPosition < _utf8Offset && _utf16Enumerator.MoveNext() && _utf16Enumerator.Current != '\0') { }

			return _state.utf8Position - startPosition;
		}

		public static int AdvanceUtf16EnumeratorByUtf8Bytes(IEnumerator<char> _utf16Enumerator, int _utf8Offset)
		{
			if (_utf16Enumerator == null || _utf8Offset <= 0)
			{
				return 0;
			}

			int curOffset = 0;

			IEnumerator<int> e = ReadCodepointsFromUtf16Enumerator(_utf16Enumerator);
			while (curOffset < _utf8Offset && e.MoveNext() && e.Current != 0)
			{
				curOffset += GetUtf8ByteCountForCodepoint(e.Current);
			}

			return curOffset;
		}

		public static int GetUtf8ByteCountForCodepoint(int _codepoint)
		{
			// Single-byte UTF-8/ASCII character, starting at U+0:
			if (_codepoint < 0x80)
			{
				return 1;
			}
			// 2 bytes, starting at U+80:
			else if (_codepoint < 0x800)
			{
				return 2;
			}
			// 3 bytes, starting at U+800:
			else if (_codepoint < 0x10000)
			{
				return 3;
			}
			// 4 bytes, starting at U+10000:
			else
			{
				return 4;
			}
		}

		public static int FindStringInUtf8Bytes(byte[] _utf8Bytes, int _utf8Length, string _query)
		{
			if (string.IsNullOrEmpty(_query))
			{
				return -1;
			}

			CharIteratorState state = new();

			IEnumerator<char> e = ReadUtf16FromUtf8Bytes(_utf8Bytes, state, _utf8Length);

			if (FindStringInUtf16Enumerator(e, _query) < 0)
			{
				return -1;
			}

			return state.utf8Position;
		}

		public static int FindStringInUtf16Enumerator(IEnumerator<char> _utf16Enumerator, string _query)
		{
			if (string.IsNullOrEmpty(_query))
			{
				return -1;
			}

			char queryStartChar = _query[0];

			// Iterate over all characters while converting to UTF-16 on demand:
			char c;
			int position = 0;
			while (_utf16Enumerator.MoveNext() && (c = _utf16Enumerator.Current) != '\0')
			{
				if (c == queryStartChar)
				{
					int i = 1;
					while (i < _query.Length && _utf16Enumerator.MoveNext() && _utf16Enumerator.Current == _query[i]) { }

					// If the above loop ran through until the end, the query was matched:
					if (i == _query.Length)
					{
						return position;
					}
				}
				position++;
			}

			return -1;
		}

		#endregion
	}
}

