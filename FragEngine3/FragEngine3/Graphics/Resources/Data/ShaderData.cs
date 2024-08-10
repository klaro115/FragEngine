using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources.Data;
using FragEngine3.Utility;
using System.Reflection.PortableExecutable;
using System.Text;

namespace FragEngine3.Graphics.Resources.Data;

[Serializable]
[ResourceDataType(typeof(ShaderResource))]
public sealed class ShaderData
{
	#region Properties

	// GENERAL:

	public ShaderDataFileHeader FileHeader { get; set; } = new();
	public ShaderDescriptionData Description { get; init; } = ShaderDescriptionData.none;

	// SOURCE CODE:

	public byte[]? SourceCode = null;

	// COMPILED BYTE CODE:

	public byte[]? ByteCodeDxbc { get; init; } = null;
	public byte[]? ByteCodeDxil { get; init; } = null;
	public byte[]? ByteCodeSpirv { get; init; } = null;
	//...

	#endregion
	#region Methods

	public bool IsValid()
	{
		if (Description is null || !Description.IsValid())
		{
			return false;
		}
		// Data must contain compiled byte code of any kind, or at least source code:
		if (ByteCodeDxbc is null && ByteCodeDxil is null && ByteCodeSpirv is null)
		{
			return !FileHeader.sourceCode.IsEmpty();
		}
		return true;
	}

	public static bool Read(BinaryReader _reader, out ShaderData? _outData, CompiledShaderDataType _typeFlags = CompiledShaderDataType.ALL)
	{
		if (_reader is null)
		{
			Logger.Instance?.LogError("Cannot read shader data using null binary reader!");
			_outData = null;
			return false;
		}

		long fileStartPosition = _reader.BaseStream.Position;

		// Parse file header:
		if (!ShaderDataFileHeader.Read(_reader, out ShaderDataFileHeader fileHeader))
		{
			_outData = null;
			return false;
		}

		// Read shader description JSON:
		long jsonStartPosition = fileStartPosition + fileHeader.jsonDescription.byteOffset;
		_reader.JumpToPosition(jsonStartPosition);

		// Read and deserialize JSON:
		if (!ShaderDescriptionData.Read(_reader, fileHeader.jsonDescription.byteSize, out ShaderDescriptionData description))
		{
			_outData = null;
			return false;
		}

		// Try reading source code data, if available:
		byte[]? sourceCodeUtf8Bytes = null;
		if (!fileHeader.sourceCode.IsEmpty())
		{
			long sourceCodeStartPosition = fileStartPosition + fileHeader.sourceCode.byteOffset;
			_reader.JumpToPosition(sourceCodeStartPosition);

			if (!ReadUtf8Bytes(_reader, fileHeader.sourceCode.byteSize, out sourceCodeUtf8Bytes))
			{
				_outData = null;
				return false;
			}
		}

		//... (reserved for additional headers)

		// Read compiled shader data:
		if (!ReadCompiledShaderByteCode(
			_reader,
			in fileHeader,
			description,
			_typeFlags,
			out byte[]? byteCodeDxbc,
			out byte[]? byteCodeDxil,
			out byte[]? byteCodeSpirv))
		{
			_outData = null;
			return false;
		}

		// Assemble data object:
		_outData = new()
		{
			// General:
			FileHeader = fileHeader,
			Description = description,

			// Source code:
			SourceCode = sourceCodeUtf8Bytes,

			// Compiled byte code:
			ByteCodeDxbc = byteCodeDxbc,
			ByteCodeDxil = byteCodeDxil,
			ByteCodeSpirv = byteCodeSpirv,
		};
		return _outData.IsValid();
	}

	public bool Write(BinaryWriter _writer, bool _bundleSourceCode = false)
	{
		if (_writer is null)
		{
			Logger.Instance?.LogError("Cannot write shader data using null binary writer!");
			return false;
		}

		// Serialize description to JSON:
		if (!Description.Write(out byte[] jsonUtf8Bytes))
		{
			return false;
		}
		int jsonByteSize = jsonUtf8Bytes.Length;

		bool hasSourceCode = _bundleSourceCode && SourceCode is not null && SourceCode.Length != 0;

		if (!RecalculateOffsetsAndSizes((ushort)jsonByteSize, hasSourceCode))
		{
			return false;
		}

		// Write file header:
		if (!FileHeader.Write(_writer))
		{
			return false;
		}

		// Write description JSON:
		if (!WriteByteData(_writer, jsonUtf8Bytes, jsonByteSize))
		{
			return false;
		}

		//... (reserved for additional headers)

		// Write HLSL source code, if available:
		if (hasSourceCode)
		{
			_writer.Write(SourceCode!);
		}

		bool success = true;

		// Write compiled shader data:
		if (ByteCodeDxbc is not null && ByteCodeDxbc.Length != 0)
		{
			success &= WriteByteData(_writer, ByteCodeDxbc, ByteCodeDxbc.Length);
		}
		if (ByteCodeDxil is not null && ByteCodeDxil.Length != 0)
		{
			success &= WriteByteData(_writer, ByteCodeDxil, ByteCodeDxil.Length);
		}
		if (ByteCodeSpirv is not null && ByteCodeSpirv.Length != 0)
		{
			success &= WriteByteData(_writer, ByteCodeSpirv, ByteCodeSpirv.Length);
		}
		//...

		return success;
	}

	private bool RecalculateOffsetsAndSizes(ushort _jsonByteSize, bool _hasSourceCode)
	{
		ShaderDataFileHeader fileHeader = FileHeader;

		// Pre-calculate all sizes and offsets:
		ushort jsonByteOffset = ShaderDataFileHeader.minFileHeaderSize;
		ushort sourceCodeOffset = 0;
		uint sourceCodeSize = 0;
		if (_hasSourceCode)
		{
			sourceCodeOffset = (ushort)(jsonByteOffset + _jsonByteSize);
			sourceCodeSize = (uint)SourceCode!.Length;
		}
		uint shaderDataOffset = sourceCodeOffset + sourceCodeSize;
		uint shaderDataByteSize = 0;
		if (ByteCodeDxbc is not null) shaderDataByteSize += (uint)ByteCodeDxbc.Length;
		if (ByteCodeDxil is not null) shaderDataByteSize += (uint)ByteCodeDxil.Length;
		if (ByteCodeSpirv is not null) shaderDataByteSize += (uint)ByteCodeSpirv.Length;

		// Update sizes and offsets in header:
		fileHeader.fileHeaderSize = ShaderDataFileHeader.minFileHeaderSize;
		fileHeader.jsonDescription = new()
		{
			byteOffset = jsonByteOffset,
			byteSize = _jsonByteSize,
		};
		fileHeader.sourceCode = new()
		{
			byteOffset = sourceCodeOffset,
			byteSize = sourceCodeSize,
		};
		fileHeader.shaderData = new()
		{
			byteOffset = shaderDataOffset,
			byteSize = shaderDataByteSize,
		};

		FileHeader = fileHeader;
		return true;
	}

	private static bool ReadCompiledShaderByteCode(
		BinaryReader _reader,
		in ShaderDataFileHeader _fileHeader,
		ShaderDescriptionData _desc,
		CompiledShaderDataType _typeFlags,
		out byte[]? _outByteCodeDxbc,
		out byte[]? _outByteCodeDxil,
		out byte[]? _outByteCodeSpirv)
	{
		_outByteCodeDxbc = null;
		_outByteCodeDxil = null;
		_outByteCodeSpirv = null;

		foreach (var compiledShaderData in _desc.CompiledShaders)
		{
			CompiledShaderDataType type = compiledShaderData.type;
			if (!_typeFlags.HasFlag(type))
				continue;

			if (!_desc.GetCompiledShaderByteOffsetAndSize(type, _fileHeader.shaderData.byteOffset, out uint dataStartPosition, out uint expectedByteSize))
				continue;

			try
			{
				_reader.JumpToPosition(dataStartPosition);

				byte[] byteCode = new byte[expectedByteSize];
				int actualByteSize = _reader.Read(byteCode, 0, (int)expectedByteSize);
				if (actualByteSize < expectedByteSize)
				{
					byte[] trimmedByteCode = new byte[actualByteSize];
					Array.Copy(byteCode, trimmedByteCode, actualByteSize);
					byteCode = trimmedByteCode;
				}

				switch (type)
				{
					case CompiledShaderDataType.DXBC:
						_outByteCodeDxbc = byteCode;
						break;
					case CompiledShaderDataType.DXIL:
						_outByteCodeDxil = byteCode;
						break;
					case CompiledShaderDataType.SPIRV:
						_outByteCodeSpirv = byteCode;
						break;
					default:
						break;
				}
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Reading compiled shader data has failed on byte code type '{type}'!", ex);
				return false;
			}
		}
		return true;
	}

	public static bool ReadUtf8Bytes(BinaryReader _reader, uint _expectedByteSize, out byte[] _outUtf8Bytes)
	{
		if (_reader is null)
		{
			_outUtf8Bytes = [];
			return false;
		}
		if (_expectedByteSize == 0)
		{
			_outUtf8Bytes = [];
			return true;
		}

		try
		{
			_outUtf8Bytes = new byte[_expectedByteSize];

			int actualByteSize = _reader.Read(_outUtf8Bytes, 0, (int)_expectedByteSize);
			if (actualByteSize != _expectedByteSize)
			{
				byte[] actualSourceCodeUtf8Bytes = new byte[actualByteSize];
				Array.Copy(_outUtf8Bytes, actualSourceCodeUtf8Bytes, actualByteSize);
				_outUtf8Bytes = actualSourceCodeUtf8Bytes;
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Reading UTF-8 byte string from shader data has failed!", ex);
			_outUtf8Bytes = [];
			return false;
		}
	}

	public static bool WriteUtf8Bytes(BinaryWriter _writer, string _text, out uint _outByteSize)
	{
		if (_writer is null)
		{
			_outByteSize = 0;
			return false;
		}
		if (string.IsNullOrEmpty(_text))
		{
			_outByteSize = 0;
			return true;
		}

		try
		{
			byte[] utf8Bytes = Encoding.UTF8.GetBytes(_text);
			_writer.Write(utf8Bytes);

			_outByteSize = (uint)utf8Bytes.Length;
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Writing UTF-8 byte string for shader data has failed!", ex);
			_outByteSize = 0;
			return false;
		}
	}

	public static bool WriteByteData(BinaryWriter _writer, byte[] _bytes, int _byteSize)
	{
		if (_writer is null)
		{
			return false;
		}
		if (_bytes is null || _bytes.Length == 0)
		{
			return true;
		}

		try
		{
			_writer.Write(_bytes, 0, _byteSize);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Writing byte data for shader data has failed!", ex);
			return false;
		}
	}

	#endregion
}
