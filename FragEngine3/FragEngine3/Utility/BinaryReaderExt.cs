namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for the <see cref="BinaryReader"/> type.
/// </summary>
public static class BinaryReaderExt
{
	#region Methods

	/// <summary>
	/// Jump the reader's current position on its underlying stream to a specific position.
	/// </summary>
	/// <param name="_reader">This binary reader.</param>
	/// <param name="_targetPosition">A position on the underlying stream that we want to jump to for subsequent reading, in bytes. May not be negative or out-of-bounds.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the target position is out of range.</exception>
	/// <exception cref="NotSupportedException">Thrown if seeking is not supported by the stream, but target position lies before current read position.</exception>
	public static void JumpToPosition(this BinaryReader _reader, long _targetPosition)
	{
		if (_targetPosition < 0 || _targetPosition > _reader.BaseStream.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(_targetPosition), _targetPosition, "Jump target position for BinaryReader was out of range.");
		}

		// No need to jump if we're already on target:
		if (_reader.BaseStream.Position == _targetPosition)
		{
			return;
		}

		// If seeking is supported, jump straight to target position:
		if (_reader.BaseStream.CanSeek)
		{
			_reader.BaseStream.Position = _targetPosition;
			return;
		}
		
		// If seekig is not supported, we can only move forward:
		if (_reader.BaseStream.Position > _targetPosition)
		{
			throw new NotSupportedException("BinaryReader does not support seeking, and target position was less than current position.");
		}

		// Read in blocks of 64-bit until close to target position, then finish last few steps in byte steps:
		long i;
		long blockCount = (_targetPosition - _reader.BaseStream.Position) >> 3;
		for (i = 0; i < blockCount; ++i)
		{
			_reader.ReadUInt64();
		}
		for (i = _reader.BaseStream.Position; i < _targetPosition; ++i)
		{
			_reader.ReadByte();
		}
		return;
	}

	#endregion
	#region Methods Hexadecimal

	/// <summary>
	/// Reads 2 hexadecimal ASCII characters and parses them into an 8-bit <see cref="byte"/>.
	/// </summary>
	/// <param name="_reader">This reader.</param>
	/// <returns>An 8-bit byte value.</returns>
	public static byte ReadHexUint8(this BinaryReader _reader)
	{
		uint c0 = ConvertHexToValue(_reader.ReadByte());
		uint c1 = ConvertHexToValue(_reader.ReadByte());
		return (byte)(c0 << 4 | c1);
	}

	/// <summary>
	/// Reads 4 hexadecimal ASCII characters and parses them into a 16-bit <see cref="ushort"/>.
	/// </summary>
	/// <param name="_reader">This reader.</param>
	/// <returns>A 16-bit unsigned short value.</returns>
	public static ushort ReadHexUint16(this BinaryReader _reader)
	{
		uint c0 = ConvertHexToValue(_reader.ReadByte());
		uint c1 = ConvertHexToValue(_reader.ReadByte());
		uint c2 = ConvertHexToValue(_reader.ReadByte());
		uint c3 = ConvertHexToValue(_reader.ReadByte());
		return (ushort)(c0 << 12 | c1 << 8 | c2 << 4 | c3);
	}

	/// <summary>
	/// Reads 8 hexadecimal ASCII characters and parses them into a 32-bit <see cref="uint"/>.
	/// </summary>
	/// <param name="_reader">This reader.</param>
	/// <returns>An 32-bit unsigned integer value.</returns>
	public static uint ReadHexUint32(this BinaryReader _reader)
	{
		uint c0 = ConvertHexToValue(_reader.ReadByte());
		uint c1 = ConvertHexToValue(_reader.ReadByte());
		uint c2 = ConvertHexToValue(_reader.ReadByte());
		uint c3 = ConvertHexToValue(_reader.ReadByte());
		uint c4 = ConvertHexToValue(_reader.ReadByte());
		uint c5 = ConvertHexToValue(_reader.ReadByte());
		uint c6 = ConvertHexToValue(_reader.ReadByte());
		uint c7 = ConvertHexToValue(_reader.ReadByte());
		return c0 << 28 | c1 << 24 | c2 << 20 | c3 << 16 | c4 << 12 | c5 << 8 | c6 << 4 | c7;
	}

	private static uint ConvertHexToValue(byte _hexChar)
	{
		return _hexChar >= 'A'
			? (uint)(_hexChar - 'A' + 10)
			: (uint)(_hexChar - '0');
	}

	#endregion
}
