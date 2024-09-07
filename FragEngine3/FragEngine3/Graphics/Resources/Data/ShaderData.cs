using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources.Data;
using FragEngine3.Utility;
using System.Text;

namespace FragEngine3.Graphics.Resources.Data;

[Serializable]
[ResourceDataType(typeof(ShaderResource))]
public sealed class ShaderData
{
	#region Fields

	private static readonly byte[] SECTION_SPACER =
	[
		(byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'\r', (byte)'\n',
	];
	private static readonly uint SECTION_SPACER_SIZE = (uint)SECTION_SPACER.Length * sizeof(byte);

	#endregion
	#region Properties

	// GENERAL:

	public ShaderDataFileHeader FileHeader { get; set; } = new();
	public ShaderDescriptionData Description { get; init; } = ShaderDescriptionData.none;

	// SOURCE CODE:

	public Dictionary<ShaderLanguage, byte[]>? SourceCode = null;

	// COMPILED BYTE CODE:

	public byte[]? ByteCodeDxbc { get; init; } = null;
	public byte[]? ByteCodeDxil { get; init; } = null;
	public byte[]? ByteCodeSpirv { get; init; } = null;
	public byte[]? ByteCodeMetal { get; init; } = null;
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
			return !FileHeader.sourceCode.IsEmpty() && SourceCode is not null && SourceCode.Count != 0;
		}
		return true;
	}

	public byte[]? GetByteCodeOfType(CompiledShaderDataType _dataType)
	{
		return _dataType switch
		{
			CompiledShaderDataType.DXBC => ByteCodeDxbc,
			CompiledShaderDataType.DXIL => ByteCodeDxil,
			CompiledShaderDataType.SPIRV => ByteCodeSpirv,
			CompiledShaderDataType.MetalArchive => ByteCodeMetal,
			_ => null,
		};
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

		//... (reserved for additional headers)

		// Try reading source code data, if available:
		Dictionary<ShaderLanguage, byte[]>? sourceCodeUtf8Blocks = null;
		if (!fileHeader.sourceCode.IsEmpty() && description.SourceCode?.SourceCodeBlocks is not null)
		{
			long sourceCodeStartPosition = fileStartPosition + fileHeader.sourceCode.byteOffset;
			sourceCodeUtf8Blocks = [];

			foreach (var block in description.SourceCode.SourceCodeBlocks)
			{
				long blockStartPosition = sourceCodeStartPosition + block.ByteOffset;
				_reader.JumpToPosition(blockStartPosition);

				if (!ReadUtf8Bytes(_reader, block.ByteSize, out byte[] blockUtf8Bytes))
				{
					_outData = null;
					continue;
				}

				sourceCodeUtf8Blocks.Add(block.Language, blockUtf8Bytes);
			}
		}

		// Read compiled shader data:
		long compiledDataStartPosition = fileStartPosition + fileHeader.shaderData.byteOffset;
		if (!ReadCompiledShaderByteCode(
			_reader,
			description,
			_typeFlags,
			compiledDataStartPosition,
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
			SourceCode = sourceCodeUtf8Blocks,

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

		bool hasSourceCode = _bundleSourceCode && SourceCode is not null && SourceCode.Count != 0;

		if (!RecalculateOffsetsAndSizes((ushort)jsonByteSize, hasSourceCode))
		{
			return false;
		}

		// Write file header:
		if (!FileHeader.Write(_writer))
		{
			return false;
		}
		_writer.Write(SECTION_SPACER);

		// Write description JSON:
		if (!WriteByteData(_writer, jsonUtf8Bytes, jsonByteSize))
		{
			return false;
		}
		_writer.Write(SECTION_SPACER);

		//... (reserved for additional headers)

		// Write HLSL source code, if available:
		if (hasSourceCode)
		{
			foreach (var kvp in SourceCode!)
			{
				_writer.Write(kvp.Value);
				_writer.Write(SECTION_SPACER);
			}
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
		uint jsonByteOffset = ShaderDataFileHeader.minFileHeaderSize + SECTION_SPACER_SIZE;
		uint sourceCodeOffset = 0;
		uint sourceCodeSize = 0;
		if (_hasSourceCode && SourceCode is not null)
		{
			sourceCodeOffset = jsonByteOffset + _jsonByteSize + SECTION_SPACER_SIZE;
			foreach (var kvp in SourceCode)
			{
				uint blockSize = (uint)kvp.Value.Length + SECTION_SPACER_SIZE;
				sourceCodeSize += blockSize;
			}
		}
		uint shaderDataOffset = sourceCodeOffset + sourceCodeSize + SECTION_SPACER_SIZE;
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
		ShaderDescriptionData _desc,
		CompiledShaderDataType _typeFlags,
		long _shaderDataStartPosition,
		out byte[]? _outByteCodeDxbc,
		out byte[]? _outByteCodeDxil,
		out byte[]? _outByteCodeSpirv)
	{
		const int readBufferCapacity = 2048;

		if (_desc.CompiledVariants is null || _desc.CompiledVariants.Length == 0)
		{
			_outByteCodeDxbc = null;
			_outByteCodeDxil = null;
			_outByteCodeSpirv = null;
			return false;
		}

		List<byte>? bufferListDxbc = null;
		List<byte>? bufferListDxil = null;
		List<byte>? bufferListSpirv = null;
		byte[] byteBuffer = new byte[readBufferCapacity];

		foreach (ShaderDescriptionVariantData variantData in _desc.CompiledVariants)
		{
			if (!_typeFlags.HasFlag(variantData.Type)) continue;
			if (!variantData.IsValid()) continue;

			List<byte>? bufferList = GetBufferList(variantData.Type);
			if (bufferList is null) continue;

			uint variantStartIdx = (uint)bufferList.Count;

			try
			{
				// Jump to compiled code's start position on the reader stream:
				long variantStartPosition = _shaderDataStartPosition + variantData.ByteOffset;
				_reader.JumpToPosition(variantStartPosition);

				// Read all variant bytes from stream into buffer list:
				int remainingSize = (int)variantData.ByteSize;
				while (remainingSize > readBufferCapacity)
				{
					_reader.Read(byteBuffer, 0, readBufferCapacity);
					bufferList.AddRange(byteBuffer);
					remainingSize -= readBufferCapacity;
				}
				if (remainingSize != 0)
				{
					_reader.Read(byteBuffer, 0, remainingSize);
					ReadOnlySpan<byte> span = new(byteBuffer, 0, remainingSize);
					bufferList.AddRange(span);
				}

				// Update raw variant data to use imported array indices instead of file byte offsets:
				variantData.ByteOffset = variantStartIdx;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Reading compiled shader data has failed on byte code type '{variantData.Type}'!", ex);
				_outByteCodeDxbc = null;
				_outByteCodeDxil = null;
				_outByteCodeSpirv = null;
				return false;
			}
		}

		_outByteCodeDxbc = bufferListDxbc?.ToArray();
		_outByteCodeDxil = bufferListDxil?.ToArray();
		_outByteCodeSpirv = bufferListSpirv?.ToArray();
		return true;


		// Local helper method for fetching the right buffer list for a given shader data type:
		List<byte>? GetBufferList(CompiledShaderDataType _type)
		{
			return _type switch
			{
				CompiledShaderDataType.DXBC => bufferListDxbc ??= new(readBufferCapacity),
				CompiledShaderDataType.DXIL => bufferListDxil ??= new(readBufferCapacity),
				CompiledShaderDataType.SPIRV => bufferListSpirv ??= new(readBufferCapacity),
				_ => null,
			};
		}
	}

	public static bool ReadUtf8Bytes(BinaryReader _reader, uint _expectedByteSize, out string _outText)
	{
		if (!ReadUtf8Bytes(_reader, _expectedByteSize, out byte[] utf8Bytes))
		{
			_outText = string.Empty;
			return false;
		}

		try
		{
			_outText = Encoding.UTF8.GetString(utf8Bytes);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Reading UTF-8 string for shader data has failed!", ex);
			_outText = string.Empty;
			return false;
		}
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
