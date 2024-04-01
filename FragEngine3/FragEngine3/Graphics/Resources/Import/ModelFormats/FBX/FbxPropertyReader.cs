namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

public static class FbxPropertyReader
{

	#region Methods

	public static bool ReadProperty(BinaryReader _reader, out FbxProperty _outProperty)
	{
		if (_reader is null)
		{
			_outProperty = null!;
			return false;
		}

		byte type = _reader.ReadByte();

		if (type == 'S' || type == 'R')
		{
			return ReadProperty_Raw(_reader, out _outProperty);
		}
		else if (type < 'Z')
		{
			return ReadProperty_Primitive(_reader, (FbxPropertyType)type, out _outProperty);
		}
		else
		{
			return ReadProperty_Array(_reader, type, out _outProperty);
		}
	}

	private static bool ReadProperty_Raw(BinaryReader _reader, out FbxProperty _outProperty)
	{
		uint length = _reader.ReadUInt32();

		byte[] raw = _reader.ReadBytes((int)length);

		_outProperty = new FbxPropertyRaw(raw);
		return true;
	}

	private static bool ReadProperty_Primitive(BinaryReader _reader, FbxPropertyType _primitiveType, out FbxProperty _outProperty)
	{
		_outProperty = _primitiveType switch
		{
			FbxPropertyType.Boolean => new FbxProperty<bool>(_reader.ReadByte() != 0, FbxPropertyType.Boolean),
			FbxPropertyType.Int16 => new FbxProperty<bool>(_reader.ReadInt16() != 0, FbxPropertyType.Int16),
			FbxPropertyType.Int32 => new FbxProperty<bool>(_reader.ReadInt32() != 0, FbxPropertyType.Int32),
			FbxPropertyType.Int64 => new FbxProperty<bool>(_reader.ReadInt64() != 0, FbxPropertyType.Int64),
			FbxPropertyType.Float => new FbxProperty<bool>(_reader.ReadSingle() != 0, FbxPropertyType.Float),
			FbxPropertyType.Double => new FbxProperty<bool>(_reader.ReadDouble() != 0, FbxPropertyType.Double),
			_ => null!,
		};
		return _outProperty is not null;
	}

	private static uint GetListElementSize(byte _type)
	{
		return (char)_type switch
		{
			'Y' => 2,
			'C' => 1,
			'B' => 1,
			'I' => 4,
			'F' => 4,
			'D' => 8,
			'L' => 8,
			_ => 0,
		};
	}

	private static bool ReadProperty_Array(BinaryReader _reader, byte _type, out FbxProperty _outProperty)
	{
		uint arrayLength = _reader.ReadUInt32();
		uint encoding = _reader.ReadUInt32();
		uint compressedLength = _reader.ReadUInt32();

		byte elementType = (byte)(_type - ('a' - 'A'));
		FbxPropertyType elementPrimitiveType = (FbxPropertyType)elementType;

		FbxProperty[] properties = new FbxProperty[arrayLength];

		if (encoding != 0)
		{
			ulong decompressedLength = GetListElementSize(elementType) * arrayLength;
			byte[] decompressedBytes = new byte[decompressedLength];

			byte[] compressedBytes = _reader.ReadBytes((int)compressedLength);

			//TODO: Implement/Use zLib decompression, then actually decompress buffer here.

			using MemoryStream arrayStream = new(decompressedBytes);
			using BinaryReader arrayReader = new(arrayStream);

			for (uint i = 0; i < arrayLength; ++i)
			{
				if (ReadProperty_Primitive(arrayReader, elementPrimitiveType, out FbxProperty arrayElement))
				{
					properties[i] = arrayElement;
				}
			}
		}
		else
		{
			for (uint i = 0; i < arrayLength; ++i)
			{
				if (ReadProperty_Primitive(_reader, elementPrimitiveType, out FbxProperty arrayElement))
				{
					properties[i] = arrayElement;
				}
			}
		}

		_outProperty = new FbxPropertyArray(properties);
		return true;
	}

	#endregion
}

