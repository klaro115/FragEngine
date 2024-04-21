using FragEngine3.EngineCore;
using System.IO.Compression;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

internal static class FbxPropertyReader
{
	#region Types

	private struct ArrayHeader
	{
		public uint arrayLength;
		public uint encoding;
		public uint compressedLength;
	}

	private delegate T FuncReadPrimitiveValue<T>(BinaryReader _reader) where T : unmanaged;

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

		if (type == 'R')
		{
			return ReadProperty_Raw(_reader, out _outProperty);
		}
		else if (type == 'S')
		{
			return ReadProperty_String(_reader, out _outProperty);
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

	private static bool ReadProperty_String(BinaryReader _reader, out FbxProperty _outProperty)
	{
		uint length = _reader.ReadUInt32();

		byte[] utf8Bytes = _reader.ReadBytes((int)length);
		string text = System.Text.Encoding.UTF8.GetString(utf8Bytes);

		_outProperty = new FbxPropertyString(text);
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
		ArrayHeader header = new()
		{
			arrayLength = _reader.ReadUInt32(),
			encoding = _reader.ReadUInt32(),
			compressedLength = _reader.ReadUInt32(),
		};
		
		char elementType = (char)(_type - ('a' - 'A'));
		FbxPropertyType elementPrimitiveType = GetTypeFromChar(elementType);

		_outProperty = null!;

		return elementPrimitiveType switch
		{
			FbxPropertyType.Boolean => ReadProperty_Array(_reader, in header, FuncReadPrimitive_Bool, elementPrimitiveType, sizeof(bool), out _outProperty),
			FbxPropertyType.Int16 => ReadProperty_Array(_reader, in header, FuncReadPrimitive_Int16, elementPrimitiveType, sizeof(short), out _outProperty),
			FbxPropertyType.Int32 => ReadProperty_Array(_reader, in header, FuncReadPrimitive_Int32, elementPrimitiveType, sizeof(int), out _outProperty),
			FbxPropertyType.Int64 => ReadProperty_Array(_reader, in header, FuncReadPrimitive_Int64, elementPrimitiveType, sizeof(long), out _outProperty),
			FbxPropertyType.Float => ReadProperty_Array(_reader, in header, FuncReadPrimitive_Float, elementPrimitiveType, sizeof(float), out _outProperty),
			FbxPropertyType.Double => ReadProperty_Array(_reader, in header, FuncReadPrimitive_Double, elementPrimitiveType, sizeof(double), out _outProperty),
			_ => false,
		};


		// Local helper methods for parsing primitives from byte stream:
		static bool FuncReadPrimitive_Bool(BinaryReader _reader) => _reader.ReadByte() != 0;
		static short FuncReadPrimitive_Int16(BinaryReader _reader) => _reader.ReadInt16();
		static int FuncReadPrimitive_Int32(BinaryReader _reader) => _reader.ReadInt32();
		static long FuncReadPrimitive_Int64(BinaryReader _reader) => _reader.ReadInt16();
		static float FuncReadPrimitive_Float(BinaryReader _reader) => _reader.ReadSingle();
		static double FuncReadPrimitive_Double(BinaryReader _reader) => _reader.ReadDouble();
	}
	
	private static bool ReadProperty_Array<T>(
		BinaryReader _reader,
		in ArrayHeader _arrayHeader,
		FuncReadPrimitiveValue<T> _funcReadPrimitiveValue,
		FbxPropertyType _elementPrimitiveType,
		uint _elementByteSize,
		out FbxProperty _outProperty) where T : unmanaged
	{
		T[] properties = new T[_arrayHeader.arrayLength];

		if (_arrayHeader.encoding != 0)
		{
			ulong decompressedLength = _elementByteSize * _arrayHeader.arrayLength;
			byte[] decompressedBytes = new byte[decompressedLength];

			// Decompress contents using zLib:
			try
			{
				byte[] compressedBytes = _reader.ReadBytes((int)_arrayHeader.compressedLength);

				using MemoryStream compressedStream = new(compressedBytes, false);
				using ZLibStream decompressor = new(compressedStream, CompressionMode.Decompress, false);
				using MemoryStream decompressedStream = new(decompressedBytes, true);

				decompressor.CopyTo(decompressedStream, (int)decompressedLength);
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to decompress property array data! (Type: '{_elementPrimitiveType}', Length: {_arrayHeader.arrayLength})", ex);
				_outProperty = null!;
				return false;
			}

			using MemoryStream arrayStream = new(decompressedBytes);
			using BinaryReader arrayReader = new(arrayStream);

			for (uint i = 0; i < _arrayHeader.arrayLength; ++i)
			{
				properties[i] = _funcReadPrimitiveValue(arrayReader);
			}
		}
		else
		{
			for (uint i = 0; i < _arrayHeader.arrayLength; ++i)
			{
				properties[i] = _funcReadPrimitiveValue(_reader);
			}
		}

		_outProperty = new FbxPropertyArray<T>(properties);
		return true;
	}

	#endregion
}
