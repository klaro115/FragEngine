using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Graphics.Resources.Shaders.Internal;

namespace FragAssetFormats.Shaders.FSHA;

/// <summary>
/// Exporter for the FSHA shader asset container format.
/// </summary>
public static class FshaExporter
{
	#region Fields

	private static readonly byte[] SECTION_SPACER = [ (byte)'\r', (byte)'\n', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'\r', (byte)'\n' ];
	private static readonly uint SECTION_SPACER_LENGTH = (uint)SECTION_SPACER.Length * sizeof(byte);

	#endregion
	#region Methods Export

	/// <summary>
	/// Serializes shader resource data and writes it to stream in FSHA format.
	/// </summary>
	/// <param name="_exportCtx">The context object describing which data should be included in the resulting FSHA data.</param>
	/// <param name="_writer">A binary writer used to write the FSHA shader data to a stream.</param>
	/// <param name="_shaderData">The shader data that shall be exported to FSHA format.</param>
	/// <param name="_addSectionSpacer">Whether to insert spacer strings in-between the different data sections in the output stream.</param>
	/// <returns>True if the export succeeded, false otherwise.</returns>
	public static bool ExportToFSHA(in ImporterContext _exportCtx, BinaryWriter _writer, ShaderData _shaderData, bool _addSectionSpacer = true)
	{
		if (_exportCtx is null)
		{
			Console.WriteLine("Error! Cannot write shader data using null export context!");
			return false;
		}
		if (_writer is null)
		{
			_exportCtx.Logger.LogError("Cannot write shader data to null binary writer!");
			return false;
		}
		if (!_writer.BaseStream.CanWrite)
		{
			_exportCtx.Logger.LogError("Cannot write shader data to read-only stream!");
			return false;
		}

		long fileStartPosition = _writer.BaseStream.Position;

		// Gather block data and recalculate relative offsets for description:
		bool hasSourceCode = GatherSourceCodeBlocks(
			in _exportCtx,
			_shaderData,
			_addSectionSpacer,
			out var sourceCodeBlocks,
			out ushort sourceCodeSize);

		bool hasCompiledData = GatherCompiledDataBlocks(
			in _exportCtx,
			_shaderData,
			_addSectionSpacer,
			out var compiledDataBlocks,
			out byte compiledDataBlockCount,
			out ushort compiledDataSize);

		// Serialize description to JSON:
		if (!_shaderData.Description.SerializeToJson(in _exportCtx, _writer, out byte[] descriptionJsonBytes))
		{
			return false;
		}

		// Write file header:
		if (!WriteHeaderAndCalculateOffsets(
			in _exportCtx,
			_writer,
			(ushort)descriptionJsonBytes.Length,
			hasSourceCode,
			sourceCodeSize,
			hasCompiledData,
			compiledDataBlockCount,
			compiledDataSize,
			_addSectionSpacer,
			out FshaFileHeader header))
		{
			return false;
		}
		if (_addSectionSpacer)
		{
			_writer.Write(SECTION_SPACER);
		}

		// Write description JSON:
		long descriptionStartPosition = fileStartPosition + header.JsonOffset;
		if (!AdvanceWriter(in _exportCtx, _writer, descriptionStartPosition))
		{
			return false;
		}

		_writer.Write(descriptionJsonBytes);
		if (_addSectionSpacer)
		{
			_writer.Write(SECTION_SPACER);
		}

		// Write source code blocks:
		if (hasSourceCode && !WriteSourceCodeBlocks(in _exportCtx, _writer, header, fileStartPosition, sourceCodeBlocks!, _addSectionSpacer))
		{
			return false;
		}

		// Write compiled data blocks:
		if (hasCompiledData && !WriteCompileDataBlocks(in _exportCtx, _writer, header, fileStartPosition, compiledDataBlocks!))
		{
			return false;
		}

		return true;
	}

	private static bool GatherSourceCodeBlocks(
		in ImporterContext _exportCtx,
		ShaderData _shaderData,
		bool _addSectionSpacers,
		out List<Tuple<ShaderDataSourceCodeDesc, byte[]>>? _outBlocks,
		out ushort _outSourceCodeByteSize)
	{
		_outSourceCodeByteSize = 0;
		if (_exportCtx.SupportedShaderLanguages == 0 ||
			_shaderData.SourceCode is null ||
			_shaderData.SourceCode.Count == 0 ||
			_shaderData.Description.SourceCode is null ||
			_shaderData.Description.SourceCode.Length == 0)
		{
			_outBlocks = null;
			return false;
		}

		_outBlocks = [];
		int sourceCodeBlockCount = 0;

		foreach (var sourceCodeDesc in _shaderData.Description.SourceCode)
		{
			if (!_exportCtx.SupportedShaderLanguages.HasFlag(sourceCodeDesc.Language))
				continue;
			if (!_shaderData.SourceCode.TryGetValue(sourceCodeDesc.Language, out byte[]? sourceCodeBytes))
				continue;

			ShaderDataSourceCodeDesc adjustedDesc = new(
				sourceCodeDesc.Language,
				sourceCodeDesc.VariantFlags,
				_outSourceCodeByteSize,
				(ushort)sourceCodeBytes.Length,
				sourceCodeDesc.EntryPoint);

			sourceCodeBlockCount++;
			_outSourceCodeByteSize += (ushort)sourceCodeBytes.Length;
			if (_addSectionSpacers)
			{
				_outSourceCodeByteSize += (ushort)SECTION_SPACER_LENGTH;
			}
			_outBlocks.Add(new(adjustedDesc, sourceCodeBytes));
		}
		if (sourceCodeBlockCount == 0)
		{
			_outSourceCodeByteSize = 0;
		}
		return sourceCodeBlockCount != 0;
	}

	private static bool GatherCompiledDataBlocks(
		in ImporterContext _exportCtx,
		ShaderData _shaderData,
		bool _addSectionSpacers,
		out List<Tuple<ShaderDataCompiledBlockDesc, byte[]>>? _outBlocks,
		out byte _outCompiledDataBlockCount,
		out ushort _outCompiledDataByteSize)
	{
		_outCompiledDataBlockCount = 0;
		_outCompiledDataByteSize = 0;
		if (_exportCtx.SupportedShaderLanguages == 0 ||
			_shaderData.SourceCode is null ||
			_shaderData.SourceCode.Count == 0 ||
			_shaderData.Description.CompiledBlocks is null ||
			_shaderData.Description.CompiledBlocks.Length == 0)
		{
			_outBlocks = null;
			return false;
		}

		_outBlocks = [];

		foreach (var compiledDataDesc in _shaderData.Description.CompiledBlocks)
		{
			if (!_exportCtx.SupportedShaderDataTypes.HasFlag(compiledDataDesc.DataType))
				continue;
			if (!_shaderData.TryGetByteCode(compiledDataDesc.DataType, compiledDataDesc.VariantFlags, out byte[]? byteCode))
				continue;

			ShaderDataCompiledBlockDesc adjustedDesc = new(
				compiledDataDesc.DataType,
				compiledDataDesc.VariantFlags,
				compiledDataDesc.Capabilities,
				_outCompiledDataByteSize,
				(uint)byteCode!.Length,
				compiledDataDesc.EntryPoint);

			_outCompiledDataBlockCount++;
			_outCompiledDataByteSize += (ushort)byteCode!.Length;
			if (_addSectionSpacers)
			{
				_outCompiledDataByteSize += (ushort)SECTION_SPACER_LENGTH;
			}
			_outBlocks.Add(new(adjustedDesc, byteCode));
		}
		if (_outCompiledDataBlockCount == 0)
		{
			_outCompiledDataByteSize = 0;
		}
		return _outCompiledDataBlockCount != 0;
	}

	private static bool WriteHeaderAndCalculateOffsets(
		in ImporterContext _exportCtx,
		BinaryWriter _writer,
		ushort _descriptionByteSize,
		bool _hasSourceCode,
		ushort _sourceCodeByteSize,
		bool _hasCompiledData,
		byte _compiledDataBlockCount,
		uint _compiledDataByteSize,
		bool _addSectionSpacers,
		out FshaFileHeader _outHeader)
	{
		// File header:
		ushort headerSize = FshaFileHeader.MINIMUM_HEADER_SIZE;

		// JSON description:
		ushort descriptionOffset = headerSize;
		if (_addSectionSpacers)
		{
			descriptionOffset += (ushort)SECTION_SPACER_LENGTH;
		}

		// [optional] Source code:
		ushort sourceCodeSize = 0;
		ushort sourceCodeOffset = 0;
		if (_hasSourceCode)
		{
			sourceCodeSize = _sourceCodeByteSize;
			sourceCodeOffset = (ushort)(descriptionOffset + _descriptionByteSize);
			if (_addSectionSpacers)
			{
				sourceCodeOffset += (ushort)SECTION_SPACER_LENGTH;
			}
		}

		// [optional] Compiled data:
		uint compiledDataSize = 0;
		uint compiledDataOffset = 0;
		if (_hasCompiledData)
		{
			compiledDataSize = _compiledDataByteSize;
			compiledDataOffset = _hasSourceCode
				? (uint)(sourceCodeOffset + sourceCodeSize)
				: headerSize;
			if (_addSectionSpacers)
			{
				compiledDataOffset += (ushort)SECTION_SPACER_LENGTH;
			}
		}

		// Assemble and write header section:
		_outHeader = new FshaFileHeader()
		{
			FileVersion = FshaFileHeader.Version.Current,
			HeaderSize = headerSize,

			JsonOffset = descriptionOffset,
			JsonSize = _descriptionByteSize,

			SourceCodeOffset = sourceCodeOffset,
			SourceCodeSize = sourceCodeSize,

			CompiledDataBlockCount = _compiledDataBlockCount,
			CompiledDataOffset = compiledDataOffset,
			CompiledDataSize = compiledDataSize,
		};

		try
		{
			bool success = _outHeader.WriteFshaHeader(in _exportCtx, _writer);
			return success;
		}
		catch (Exception ex)
		{
			_exportCtx.Logger.LogException("Failed to write FSHA file header to stream during export!", ex);
			return false;
		}
	}

	private static bool WriteSourceCodeBlocks(
		in ImporterContext _exportCtx,
		BinaryWriter _writer,
		FshaFileHeader _header,
		long _fileStartPosition,
		List<Tuple<ShaderDataSourceCodeDesc, byte[]>> _blocks,
		bool _addSectionSpacer)
	{
		long sourceCodeStartPosition = _fileStartPosition + _header.SourceCodeOffset;
		if (!AdvanceWriter(in _exportCtx, _writer, sourceCodeStartPosition))
		{
			return false;
		}

		try
		{
			foreach (var block in _blocks)
			{
				_writer.Write(block.Item2);
				if (_addSectionSpacer)
				{
					_writer.Write(SECTION_SPACER);
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			_exportCtx.Logger.LogException("Failed to write FSHA source code blocks to stream during export!", ex);
			return false;
		}
	}

	private static bool WriteCompileDataBlocks(
		in ImporterContext _exportCtx,
		BinaryWriter _writer,
		FshaFileHeader _header,
		long _fileStartPosition,
		List<Tuple<ShaderDataCompiledBlockDesc, byte[]>> _blocks)
	{
		long sourceCodeStartPosition = _fileStartPosition + _header.CompiledDataOffset;
		if (!AdvanceWriter(in _exportCtx, _writer, sourceCodeStartPosition))
		{
			return false;
		}

		try
		{
			foreach (var block in _blocks)
			{
				_writer.Write(block.Item2);
			}
			return true;
		}
		catch (Exception ex)
		{
			_exportCtx.Logger.LogException("Failed to write FSHA compiled data blocks to stream during export!", ex);
			return false;
		}
	}

	private static bool AdvanceWriter(in ImporterContext _exportCtx, BinaryWriter _writer, long _targetPosition)
	{
		if (_targetPosition < _writer.BaseStream.Position)
		{
			_exportCtx.Logger.LogError("Cannot advance binary writer's underlying stream to target position before current write position!");
			return false;
		}
		if (_targetPosition == _writer.BaseStream.Position)
		{
			return true;
		}
		try
		{
			// Pad skipped bytes with whitespaces:
			long offsetToTargetPos = _targetPosition - _writer.BaseStream.Position;
			for (int i = 0; i < offsetToTargetPos; i++)
			{
				_writer.Write((byte)' ');
			}
			return true;
		}
		catch (Exception ex)
		{
			_exportCtx.Logger.LogException("Failed to advance binary writer to target position!", ex);
			return false;
		}
	}

	#endregion
}
