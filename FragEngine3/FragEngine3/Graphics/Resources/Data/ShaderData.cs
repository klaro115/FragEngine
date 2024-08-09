using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources.Data;
using FragEngine3.Utility;

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

	public ShaderSourceCodeData? SourceCode { get; init; } = null;

	// COMPILED BYTE CODE:

	public byte[]? ByteCodeDxbc { get; init; } = null;
	public byte[]? ByteCodeDxil { get; init; } = null;
	public byte[]? ByteCodeSpirv { get; init; } = null;

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
			return SourceCode is not null && !SourceCode.IsEmpty();
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
		long jsonStartPosition = fileStartPosition + fileHeader.jsonByteOffset;
		_reader.JumpToPosition(jsonStartPosition);

		// Read and deserialize JSON:
		if (!ShaderDescriptionData.Read(_reader, fileHeader.jsonByteLength, out ShaderDescriptionData description))
		{
			_outData = null;
			return false;
		}

		// Try reading source code data, if available:
		ShaderSourceCodeData? sourceCodeData = null;
		if (fileHeader.sourceCodeOffset != 0 && !ShaderSourceCodeData.Read(_reader, out sourceCodeData))
		{
			_outData = null;
			return false;
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
			SourceCode = sourceCodeData,

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

		bool hasSourceCode = _bundleSourceCode && SourceCode is not null && !SourceCode.IsEmpty();

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
		_writer.Write(jsonUtf8Bytes, 0, jsonByteSize);

		// Write source code header, if available:
		if (hasSourceCode && !SourceCode!.WriteHeader(_writer))
		{
			return false;
		}

		//... (reserved for additional headers)

		// Write HLSL source code, if available:
		if (hasSourceCode && !SourceCode!.WritePayload(_writer))
		{
			return false;
		}

		//TODO: Write compiled shader data.

		return true;
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
			sourceCodeSize = ShaderSourceCodeData.HEADER_BYTE_SIZE + (uint)SourceCode!.HlslByteLength;
		}
		uint shaderDataOffset = sourceCodeOffset + sourceCodeSize;

		// Update sizes and offsets in header:
		fileHeader.fileHeaderSize = ShaderDataFileHeader.minFileHeaderSize;
		fileHeader.jsonByteOffset = jsonByteOffset;
		fileHeader.jsonByteLength = _jsonByteSize;
		fileHeader.sourceCodeOffset = sourceCodeOffset;
		fileHeader.shaderDataOffset = shaderDataOffset;

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

			if (!_desc.GetCompiledShaderByteOffsetAndSize(type, _fileHeader.shaderDataOffset, out uint dataStartPosition, out uint expectedByteSize))
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

	#endregion
}
