using System;
namespace FragEngine3.Utility
{
	public static class CharEnumerationTools
	{
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

			int codePoint;
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

			yield return '\0';
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

		/// <summary>
		/// Advance a UTF-16 enumerator by a given number of characters.
		/// </summary>
		/// <param name="_utf16Enumerator"></param>
		/// <param name="_utf16Offset"></param>
		/// <returns>The actual number </returns>
		public static int AdvanceUtf16Enumerator(IEnumerator<char> _utf16Enumerator, int _utf16Offset)
		{
			if (_utf16Enumerator == null)
			{
				return 0;
			}

			int curOffset = 0;
			char c;
			while (curOffset < _utf16Offset && _utf16Enumerator.MoveNext() && (c = _utf16Enumerator.Current) != '\0') { }

			return curOffset;
		}

		public static int FindStringInUtf8Bytes(byte[] _utf8Bytes, int _utf8Length, string _query)
		{
			if (string.IsNullOrEmpty(_query))
			{
				return -1;
			}

			CharIteratorState state = new();

			IEnumerator<char> e = ReadUtf16FromUtf8Bytes(_utf8Bytes, state, _utf8Length);

			if (FindStringInUtf16Enumerator(e, state, _query) < 0)
			{
				return -1;
			}

			return state.utf8Position;
		}

		public static int FindStringInUtf16Enumerator(IEnumerator<char> _utf16Enumerator, CharIteratorState _state, string _query)
		{
			if (string.IsNullOrEmpty(_query))
			{
				return -1;
			}

			char queryStartChar = _query[0];

			_state ??= CharIteratorState.Zero;

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

