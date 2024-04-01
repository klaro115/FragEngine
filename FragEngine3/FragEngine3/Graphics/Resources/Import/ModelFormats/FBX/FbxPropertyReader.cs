using FragEngine3.EngineCore;
using System.IO.Compression;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

public static class FbxPropertyReader
{
	#region Types

	//...

	#endregion
	#region Methods

	public static bool ReadProperty(BinaryReader _reader, out FbxProperty _outProperty)
	{
		if (_reader is null)
		{
			_outProperty = null!;
			return false;
		}

		char type = (char)_reader.ReadByte();

		if (type == 'S' || type == 'R')
		{
			return ReadProperty_Raw(_reader, out _outProperty);
		}
		else if (type < 'Z')
		{
			FbxPropertyType primitiveType = GetTypeFromChar(type);
			return ReadProperty_Primitive(_reader, primitiveType, out _outProperty);
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
			FbxPropertyType.Boolean => new FbxProperty<bool>(_reader.ReadByte() != 0, _primitiveType),
			FbxPropertyType.Int16 => new FbxProperty<short>(_reader.ReadInt16(), _primitiveType),
			FbxPropertyType.Int32 => new FbxProperty<int>(_reader.ReadInt32(), _primitiveType),
			FbxPropertyType.Int64 => new FbxProperty<long>(_reader.ReadInt64(), _primitiveType),
			FbxPropertyType.Float => new FbxProperty<float>(_reader.ReadSingle(), _primitiveType),
			FbxPropertyType.Double => new FbxProperty<double>(_reader.ReadDouble(), _primitiveType),
			_ => null!,
		};
		return _outProperty is not null;
	}

	private static uint GetListElementSize(char _type)
	{
		return _type switch
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

	private static FbxPropertyType GetTypeFromChar(char _typeChar)
	{
		return _typeChar switch
		{
			'Y' => FbxPropertyType.Int16,
			'C' => FbxPropertyType.Boolean,
			'B' => FbxPropertyType.Boolean,
			'I' => FbxPropertyType.Int32,
			'F' => FbxPropertyType.Float,
			'D' => FbxPropertyType.Double,
			'L' => FbxPropertyType.Int64,
			_ => 0,
		};
	}

	private static bool ReadProperty_Array(BinaryReader _reader, char _type, out FbxProperty _outProperty)
	{
		uint arrayLength = _reader.ReadUInt32();
		uint encoding = _reader.ReadUInt32();
		uint compressedLength = _reader.ReadUInt32();
		
		char elementType = (char)(_type - ('a' - 'A'));
		FbxPropertyType elementPrimitiveType = GetTypeFromChar(elementType);

		FbxProperty[] properties = new FbxProperty[arrayLength];

		bool success = true;

		if (encoding != 0)
		{
			ulong decompressedLength = GetListElementSize(elementType) * arrayLength;
			byte[] decompressedBytes = new byte[decompressedLength];

			// Decompress contents using zLib:
			try
			{
				byte[] compressedBytes = _reader.ReadBytes((int)compressedLength);

				using MemoryStream compressedStream = new(compressedBytes, false);
				using ZLibStream decompressor = new(compressedStream, CompressionMode.Decompress, false);
				using MemoryStream decompressedStream = new(decompressedBytes, true);

				decompressor.CopyTo(decompressedStream, (int)decompressedLength);
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to decompress property array data! (Type: '{elementPrimitiveType}', Length: {arrayLength})", ex);
				_outProperty = null!;
				return false;
			}

			using MemoryStream arrayStream = new(decompressedBytes);
			using BinaryReader arrayReader = new(arrayStream);

			for (uint i = 0; i < arrayLength; ++i)
			{
				if (success &= ReadProperty_Primitive(arrayReader, elementPrimitiveType, out FbxProperty arrayElement))
				{
					properties[i] = arrayElement;
				}
			}
		}
		else
		{
			for (uint i = 0; i < arrayLength; ++i)
			{
				if (success &= ReadProperty_Primitive(_reader, elementPrimitiveType, out FbxProperty arrayElement))
				{
					properties[i] = arrayElement;
				}
			}
		}

		_outProperty = new FbxPropertyArray(properties);
		return success;
	}

	#endregion
}

