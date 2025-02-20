namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for the <see cref="BinaryWriter"/> type.
/// </summary>
public static class BinaryWriterExt
{
	#region Methods

	/// <summary>
	/// Jump the writer's current position on its underlying stream to a specific position.
	/// </summary>
	/// <param name="_writer">This binary writer.</param>
	/// <param name="_targetPosition">A position on the underlying stream that we want to jump to for subsequent writing, in bytes. May not be negative or out-of-bounds.</param>
	/// <param name="_fillWithZeroIfCantSeek">If the underlying stream does not support seeking, whether to jump by writing 0-bytes to all skipped positions instead. This may
	/// overwrite existing data, so use with caution. If false, an <see cref="NotSupportedException"/> will be thrown instead.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the target position is out of range.</exception>
	/// <exception cref="NotSupportedException">Thrown if seeking is not supported by the stream, but target position lies before current write position.</exception>
	public static void JumpToPosition(this BinaryWriter _writer, long _targetPosition, bool _fillWithZeroIfCantSeek = false)
	{
		if (_targetPosition < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(_targetPosition), _targetPosition, "Jump target position for BinaryWriter was out of range.");
		}

		// No need to jump if we're already on target:
		if (_writer.BaseStream.Position == _targetPosition)
		{
			return;
		}

		// If seeking is supported, jump straight to target position:
		if (_writer.BaseStream.CanSeek)
		{
			_writer.BaseStream.Position = _targetPosition;
			return;
		}

		// If seekig is not supported, we can only move forward:
		if (!_fillWithZeroIfCantSeek)
		{
			throw new NotSupportedException("BinaryWriter does not support seeking.");
		}
		if (_writer.BaseStream.Position > _targetPosition)
		{
			throw new NotSupportedException("BinaryWriter does not support seeking, and target position was less than current position.");
		}

		// Write in blocks of 64-bit until close to target position, then finish last few steps in byte steps:
		long i;
		long blockCount = (_targetPosition - _writer.BaseStream.Position) >> 3;
		for (i = 0; i < blockCount; ++i)
		{
			const ulong padding = 0ul;
			_writer.Write(padding);
		}
		for (i = _writer.BaseStream.Position; i < _targetPosition; ++i)
		{
			const byte padding = 0x00;
			_writer.Write(padding);
		}
		return;
	}

	#endregion
	#region Methods Hexadecimal

	/// <summary>
	/// Writes an 8-bit <see cref="byte"/> value as 2 hexadecimal ASCII characters.
	/// </summary>
	/// <param name="_writer">This writer.</param>
	/// <param name="_value">An 8-bit byte value.</param>
	public static void WriteUint8ToHex(this BinaryWriter _writer, byte _value)
	{
		_writer.Write(ConvertNibbleToHex((_value & 0xF0u) >> 4));
		_writer.Write(ConvertNibbleToHex(_value & 0x0Fu));
	}

	/// <summary>
	/// Writes a 16-bit <see cref="ushort"/> value as 4 hexadecimal ASCII characters.
	/// </summary>
	/// <param name="_writer">This writer.</param>
	/// <param name="_value">A 16-bit unsigned short value.</param>
	public static void WriteUint16ToHex(this BinaryWriter _writer, ushort _value)
	{
		_writer.Write(ConvertNibbleToHex((_value & 0xF000u) >> 12));
		_writer.Write(ConvertNibbleToHex((_value & 0x0F00u) >> 8));
		_writer.Write(ConvertNibbleToHex((_value & 0x00F0u) >> 4));
		_writer.Write(ConvertNibbleToHex(_value & 0x000Fu));
	}

	/// <summary>
	/// Writes a 32-bit <see cref="uint"/> value as 8 hexadecimal ASCII characters.
	/// </summary>
	/// <param name="_writer">This writer.</param>
	/// <param name="_value">A 32-bit unsigned integer value.</param>
	public static void WriteUint32ToHex(this BinaryWriter _writer, uint _value)
	{
		_writer.Write(ConvertNibbleToHex((_value & 0xF0000000u) >> 28));
		_writer.Write(ConvertNibbleToHex((_value & 0x0F000000u) >> 24));
		_writer.Write(ConvertNibbleToHex((_value & 0x00F00000u) >> 20));
		_writer.Write(ConvertNibbleToHex((_value & 0x000F0000u) >> 16));
		_writer.Write(ConvertNibbleToHex((_value & 0x0000F000u) >> 12));
		_writer.Write(ConvertNibbleToHex((_value & 0x00000F00u) >> 8));
		_writer.Write(ConvertNibbleToHex((_value & 0x000000F0u) >> 4));
		_writer.Write(ConvertNibbleToHex(_value & 0x0000000Fu));
	}

	private static byte ConvertNibbleToHex(uint _uint4)
	{
		return _uint4 >= 10
			? (byte)(_uint4 + 'A' - 10)
			: (byte)(_uint4 + '0');
	}

	#endregion
}
