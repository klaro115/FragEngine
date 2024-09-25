namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

internal static class ShaderDataReadWriteHelper
{
	#region Methods

	public static byte ReadUInt8(BinaryReader _reader, byte[] _buffer)
	{
		_reader.Read(_buffer, 0, 2);    // 2x uppercase hex characters (0-9, A-F)
		_reader.ReadByte();             // trailing underscore ('_') or line break ('\n').
		uint value =
			(HexCharToValue(_buffer[0]) << 4) |
			(HexCharToValue(_buffer[1]) << 0);
		return (byte)(value & 0xFF);
	}

	public static ushort ReadUInt16(BinaryReader _reader, byte[] _buffer)
	{
		_reader.Read(_buffer, 0, 4);    // 4x uppercase hex characters (0-9, A-F)
		_reader.ReadByte();             // trailing underscore ('_') or line break ('\n').
		uint value =
			(HexCharToValue(_buffer[0]) << 12) |
			(HexCharToValue(_buffer[1]) << 8) |
			(HexCharToValue(_buffer[2]) << 4) |
			(HexCharToValue(_buffer[3]) << 0);
		return (ushort)(value & 0xFFFF);
	}

	public static uint ReadUInt32(BinaryReader _reader, byte[] _buffer)
	{
		_reader.Read(_buffer, 0, 8);    // 4x uppercase hex characters (0-9, A-F)
		_reader.ReadByte();             // trailing underscore ('_') or line break ('\n').
		uint value =
			(HexCharToValue(_buffer[0]) << 28) |
			(HexCharToValue(_buffer[1]) << 24) |
			(HexCharToValue(_buffer[2]) << 20) |
			(HexCharToValue(_buffer[3]) << 16) |
			(HexCharToValue(_buffer[4]) << 12) |
			(HexCharToValue(_buffer[5]) << 8) |
			(HexCharToValue(_buffer[6]) << 4) |
			(HexCharToValue(_buffer[7]) << 0);
		return (ushort)(value & 0xFFFF);
	}

	public static void WriteUInt8(BinaryWriter _writer, byte _value)
	{
		byte hex0 = ValueToHexChar((uint)(_value >> 4));
		byte hex1 = ValueToHexChar((uint)(_value >> 0));
		_writer.Write(hex0);
		_writer.Write(hex1);
	}

	public static void WriteUInt16(BinaryWriter _writer, ushort _value)
	{
		for (int i = 3; i >= 0; i--)
		{
			int shift = i * 4;
			byte hex = ValueToHexChar((uint)(_value >> shift));
			_writer.Write(hex);
		}
	}

	public static void WriteUInt32(BinaryWriter _writer, uint _value)
	{
		for (int i = 7; i >= 0; i--)
		{
			int shift = i * 4;
			byte hex = ValueToHexChar(_value >> shift);
			_writer.Write(hex);
		}
	}

	public static uint HexCharToValue(byte _x)
	{
		uint value = _x > '9'
			? _x - (uint)'A' + 10
			: _x - (uint)'0';
		return value;
	}

	public static byte ValueToHexChar(uint value)
	{
		value &= 0x0Fu;
		uint hex = value > 9
			? value + 'A' - 10
			: value + '0';
		return (byte)hex;
	}

	#endregion
}
