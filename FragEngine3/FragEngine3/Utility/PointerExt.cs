namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for pointer types.
/// </summary>
internal static unsafe class PointerExt
{
	#region Methods

	/// <summary>
	/// Tries to measure the length of a UTF-8 encoded string.
	/// </summary>
	/// <param name="pUtf8String">A pointer to a string, encoded in either UTF-8 or ASCII.</param>
	/// <param name="_maxLength">The maximum possible length of the string. This parameter exists solely to prevent enless loops and access violation exceptions.</param>
	/// <returns>The length of the string, in bytes.</returns>
	public static unsafe uint GetUtf8Length(this IntPtr pUtf8String, uint _maxLength = 4096u)
	{
		if (pUtf8String == IntPtr.Zero)
		{
			return 0;
		}

		byte* pUtf8 = (byte*)pUtf8String;
		
		uint length = 0;
		while (length < _maxLength && pUtf8[length] != (byte)'\0')
		{
			length++;
		}
		return length;
	}

	/// <summary>
	/// Tries to measure the length of a UTF-16 encoded string.
	/// </summary>
	/// <param name="pUtf16String">A pointer to a string, encoded in UTF-16.</param>
	/// <param name="_maxLength">The maximum possible length of the string. This parameter exists solely to prevent enless loops and access violation exceptions.</param>
	/// <returns>The length of the string, in code units (aka C# chars).</returns>
	public static unsafe uint GetUtf16Length(this IntPtr pUtf16String, uint _maxLength = 4096u)
	{
		if (pUtf16String == IntPtr.Zero)
		{
			return 0;
		}

		ushort* pUtf8 = (ushort*)pUtf16String;

		uint length = 0;
		while (length < _maxLength && pUtf8[length] != '\0')
		{
			length++;
		}
		return length;
	}

	#endregion
}
