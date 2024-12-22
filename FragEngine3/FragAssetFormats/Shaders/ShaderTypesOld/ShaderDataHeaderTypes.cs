namespace FragAssetFormats.Shaders.ShaderTypes;

public struct ShaderDataVersion()
{
	#region Fields

	public byte major = 1;
	public byte minor = 0;

	#endregion
	#region Methods

	public override readonly string ToString() => $"Version: {major}.{minor}";

	#endregion
}

public struct ShaderDataOffsetAndSize
{
	#region Fields

	public uint byteOffset;
	public uint byteSize;

	#endregion
	#region Properties

	public readonly bool IsEmpty() => byteOffset == 0 || byteSize == 0;

	#endregion
	#region Methods

	public static ShaderDataOffsetAndSize Read16(BinaryReader _reader, byte[] _buffer)
	{
		ShaderDataOffsetAndSize value = new()
		{
			byteOffset = ShaderDataReadWriteHelper.ReadUInt16(_reader, _buffer),
			byteSize = ShaderDataReadWriteHelper.ReadUInt16(_reader, _buffer),
		};
		return value;
	}

	public static ShaderDataOffsetAndSize Read32(BinaryReader _reader, byte[] _buffer)
	{
		ShaderDataOffsetAndSize value = new()
		{
			byteOffset = ShaderDataReadWriteHelper.ReadUInt32(_reader, _buffer),
			byteSize = ShaderDataReadWriteHelper.ReadUInt32(_reader, _buffer),
		};
		return value;
	}

	public readonly void Write16(BinaryWriter _writer)
	{
		ShaderDataReadWriteHelper.WriteUInt16(_writer, (ushort)byteOffset);
		_writer.Write((byte)'_');
		ShaderDataReadWriteHelper.WriteUInt16(_writer, (ushort)byteSize);
	}

	public readonly void Write32(BinaryWriter _writer)
	{
		ShaderDataReadWriteHelper.WriteUInt32(_writer, byteOffset);
		_writer.Write((byte)'_');
		ShaderDataReadWriteHelper.WriteUInt32(_writer, byteSize);
	}

	public override readonly string ToString() => $"Offset: {byteOffset}, Size: {byteSize}";

	#endregion
}
