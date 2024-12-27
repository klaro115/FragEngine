using FragAssetFormats.Contexts;
using FragAssetFormats.Shaders.ShaderTypes;

namespace FragAssetFormats.Shaders.FSHA;

/// <summary>
/// Importer for the FSHA shader asset container format.
/// </summary>
public static class FshaImporter
{
	#region Methods Import

	/// <summary>
	/// Reads shader resource data from stream and deserializes it from FSHA format.
	/// </summary>
	/// <param name="_importCtx">The context object describing which data should be skipped when reading FSHA data.</param>
	/// <param name="_reader">A binary reader used to read the FSHA shader data from stream.</param>
	/// <param name="_outShaderData">Outputs an object of type <see cref="ShaderData"/> after import succeeded, or null, on failure.</param>
	/// <returns>True if shader data was successfully read and parsed from FSHA format, otherwise false.</returns>
	public static bool ImportFromFSHA(in ImporterContext _importCtx, BinaryReader _reader, out ShaderData _outShaderData)
	{
		if (_importCtx is null)
		{
			Console.WriteLine("Error! Cannot read shader data using null import context!");
			goto abort;
		}
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data from null binary reader!");
			goto abort;
		}
		if (!_reader.BaseStream.CanRead)
		{
			_importCtx.Logger.LogError("Cannot read shader data from write-only stream!");
			goto abort;
		}

		long fileStartPosition = _reader.BaseStream.Position;

		// Read header:
		if (!FshaFileHeader.ReadFshaHeader(in _importCtx, _reader, out FshaFileHeader header))
		{
			goto abort;
		}

		long descriptionStartPosition = fileStartPosition + header.JsonOffset;
		if (!AdvanceReader(in _importCtx, _reader, descriptionStartPosition))
		{
			goto abort;
		}

		// Read description JSON:
		if (!ShaderDataDescription.DeserializeFromJson(in _importCtx, in header, _reader, out ShaderDataDescription? description) || description is null)
		{
			goto abort;
		}

		// Read source code:
		Dictionary<ShaderLanguage, byte[]>? sourceCodeDict = null;
		if (ShaderData.CheckHasSourceCode(in header, in description))
		{
			long sourceCodeStartPosition = fileStartPosition + header.SourceCodeOffset;
			if (!AdvanceReader(in _importCtx, _reader, sourceCodeStartPosition))
			{
				goto abort;
			}

			if (!ReadSourceCodeBlocks(in _importCtx, in header, in description, _reader, out sourceCodeDict))
			{
				goto abort;
			}
		}

		// Read compiled shader data:
		byte[]? bytesDxbc = null;
		byte[]? bytesDxil = null;
		byte[]? bytesSpirv = null;
		byte[]? bytesMetal = null;
		if (ShaderData.CheckHasCompiledData(in header, in description))
		{
			long compiledDataStartPosition = fileStartPosition + header.CompiledDataOffset;
			if (!AdvanceReader(in _importCtx, _reader, compiledDataStartPosition))
			{
				goto abort;
			}

			if (!ReadCompiledDataBlocks(in _importCtx, in header, in description, _reader, out bytesDxbc, out bytesDxil, out bytesSpirv, out bytesMetal))
			{
				goto abort;
			}
		}

		// Assemble fully read shader data object:
		_outShaderData = new()
		{
			FileHeader = header,
			Description = description,

			SourceCode = sourceCodeDict,

			ByteCodeDxbc = bytesDxbc,
			ByteCodeDxil = bytesDxil,
			ByteCodeSpirv = bytesSpirv,
			ByteCodeMetal = bytesMetal,
		};
		return true;


	abort:
		_outShaderData = null!;
		return false;
	}

	private static bool ReadSourceCodeBlocks(
		in ImporterContext _importCtx,
		in FshaFileHeader _header,
		in ShaderDataDescription _description,
		BinaryReader _reader,
		out Dictionary<ShaderLanguage, byte[]>? _outSourceCodeDict)
	{
		// Check if shader data includes source code:
		if (_header is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data source code blocks using null header!");
			_outSourceCodeDict = null;
			return false;
		}
		if (_header.SourceCodeOffset == 0 || _header.SourceCodeSize == 0)
		{
			_outSourceCodeDict = null;
			return true;
		}

		// Check if description contains valid source code definitions:
		if (_description is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data source code blocks using null description!");
			_outSourceCodeDict = null;
			return false;
		}
		int sourceCodeBlockCount = _description.SourceCode is not null
			? _description.SourceCode.Length
			: 0;
		if (sourceCodeBlockCount == 0)
		{
			_importCtx.Logger.LogWarning("Shader data contains source code data, but layout and type of source code blocks is not defined.");
			_outSourceCodeDict = null;
			return true;
		}

		long startPosition = _reader.BaseStream.Position;

		// Select and read source code blocks:
		_outSourceCodeDict = new(sourceCodeBlockCount);
		foreach (ShaderDataSourceCodeDesc sourceCodeDesc in _description.SourceCode!)
		{
			// Skip any shader languages that are not supported on current platform:
			if (!_importCtx.SupportedShaderLanguages.HasFlag(sourceCodeDesc.language))
			{
				continue;
			}

			long blockStartPosition = startPosition + sourceCodeDesc.offset;
			if (!AdvanceReader(in _importCtx, _reader, blockStartPosition))
			{
				return false;
			}

			byte[] blockBytes = new byte[sourceCodeDesc.size];
			int actualSize = _reader.Read(blockBytes, 0, blockBytes.Length);

			if (actualSize != blockBytes.Length)
			{
				byte[] newBlockBytes = new byte[actualSize];
				Array.Copy(blockBytes, newBlockBytes, actualSize);
				blockBytes = newBlockBytes;
			}

			_outSourceCodeDict.Add(sourceCodeDesc.language, blockBytes);
		}
		return true;
	}

	private static bool ReadCompiledDataBlocks(
		in ImporterContext _importCtx,
		in FshaFileHeader _header,
		in ShaderDataDescription _description,
		BinaryReader _reader,
		out byte[]? _outBytesDxbc,
		out byte[]? _outBytesDxil,
		out byte[]? _outBytesSpirv,
		out byte[]? _outBytesMetal)
	{
		_outBytesDxbc = null;
		_outBytesDxil = null;
		_outBytesSpirv = null;
		_outBytesMetal = null;

		// Check if shader data includes source code:
		if (_header is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data compiled blocks using null header!");
			return false;
		}
		if (_header.CompiledDataBlockCount == 0 || _header.CompiledDataOffset == 0 || _header.CompiledDataSize == 0)
		{
			return false;
		}

		// Check if description contains valid source code definitions:
		if (_description is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data compiled blocks using null description!");
			return false;
		}
		int compiledDataBlockCount = _description.CompiledBlocks is not null
			? _description.CompiledBlocks.Length
			: 0;
		if (compiledDataBlockCount == 0)
		{
			_importCtx.Logger.LogWarning("Shader data contains compiled data, but layout and type of source compiled blocks is not defined.");
			return false;
		}

		long startPosition = _reader.BaseStream.Position;

		foreach (ShaderDataCompiledBlockDesc compiledDesc in _description.CompiledBlocks!)
		{
			// Skip any data types that are not supported on current platform:
			if (!_importCtx.SupportedShaderDataTypes.HasFlag(compiledDesc.dataType))
			{
				continue;
			}

			long blockStartPosition = startPosition + compiledDesc.offset;
			if (!AdvanceReader(in _importCtx, _reader, blockStartPosition))
			{
				return false;
			}

			byte[] blockBytes = new byte[compiledDesc.size];
			int actualSize = _reader.Read(blockBytes, 0, blockBytes.Length);

			if (actualSize != blockBytes.Length)
			{
				byte[] newBlockBytes = new byte[actualSize];
				Array.Copy(blockBytes, newBlockBytes, actualSize);
				blockBytes = newBlockBytes;
			}

			switch (compiledDesc.dataType)
			{
				case CompiledShaderDataType.DXBC:
					_outBytesDxbc = blockBytes;
					break;
				case CompiledShaderDataType.DXIL:
					_outBytesDxil = blockBytes;
					break;
				case CompiledShaderDataType.SPIRV:
					_outBytesSpirv = blockBytes;
					break;
				case CompiledShaderDataType.MetalArchive:
					_outBytesDxbc = blockBytes;
					break;
				default:
					_importCtx.Logger.LogWarning($"Shader data contains unknown or unsupported compiled data type '{compiledDesc.dataType}'. Discarding...");
					break;
			}
		}
		return false;
	}

	private static bool AdvanceReader(in ImporterContext _importCtx, BinaryReader _reader, long _targetPosition)
	{
		if (_reader.BaseStream.Position == _targetPosition)
		{
			return true;
		}
		if (_reader.BaseStream.CanSeek)
		{
			_reader.BaseStream.Position = _targetPosition;
			return true;
		}
		if (_targetPosition > _reader.BaseStream.Position)
		{
			int skipLength = (int)(_targetPosition - _reader.BaseStream.Position);
			Span<byte> temp = stackalloc byte[skipLength];
			_reader.Read(temp);
			return true;
		}
		else
		{
			_importCtx.Logger.LogError("Unable to advance binary reader streamto target position!");
			return false;
		}
	}

	#endregion
}
