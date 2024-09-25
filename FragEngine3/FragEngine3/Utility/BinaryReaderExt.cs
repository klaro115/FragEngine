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
}
