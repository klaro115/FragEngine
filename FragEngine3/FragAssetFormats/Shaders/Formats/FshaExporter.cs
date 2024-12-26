using FragAssetFormats.Contexts;
using FragAssetFormats.Shaders.ShaderTypes;

namespace FragAssetFormats.Shaders.Formats;

public static class FshaExporter
{
	#region Fields

	private static readonly byte[] SECTION_SPACER = [(byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'#', (byte)'\r', (byte)'\n'];
	private static readonly uint SECTION_SPACER_LENGTH = (uint)SECTION_SPACER.Length * sizeof(byte);

	#endregion
	#region Methods Export

	public static bool ExportToFSHA(in ImporterContext _importCtx, BinaryWriter _writer, ShaderData _shaderData, bool _addSectionSpacer = true)
	{
		if (_importCtx is null)
		{
			Console.WriteLine("Error! Cannot write shader data using null import context!");
			return false;
		}
		if (_writer is null)
		{
			_importCtx.Logger.LogError("Cannot write shader data to null binary writer!");
			return false;
		}
		if (!_writer.BaseStream.CanWrite)
		{
			_importCtx.Logger.LogError("Cannot write shader data to read-only stream!");
			return false;
		}



		//TODO 1: Track relative positions and sizes for each part!
		//TODO 2: Add optional separator strings between parts! => "########\r\n"

		// Serialize description to JSON:
		if (!_shaderData.Description.Write(in _importCtx, _writer, out byte[] descriptionJsonBytes))
		{
			return false;
		}

		// Write file header:
		if (!WriteHeaderAndCalculateSectionSizes(in _importCtx, _writer, _shaderData, descriptionJsonBytes, _addSectionSpacer))
		{
			return false;
		}


		//TODO


		return true;
	}

	private static bool WriteHeaderAndCalculateSectionSizes(in ImporterContext _importCtx, BinaryWriter _writer, ShaderData _shaderData, byte[] _descriptionBytes, bool _addSectionSpacers)
	{
		if (_writer is null)
		{
			return false;
		}
		if (_descriptionBytes is null || _descriptionBytes.Length == 0)
		{
			_importCtx.Logger.LogError("Cannot write FSHA header using null or empty description JSON bytes!");
			return false;
		}

		uint headerSize = ShaderDataHeader.MINIMUM_HEADER_SIZE;

		// Calculate offset and size for JSON description:
		ushort descriptionOffset = (ushort)headerSize;
		ushort descriptionSize = (ushort)_descriptionBytes.Length;
		if (_addSectionSpacers)
		{
			descriptionOffset += (ushort)SECTION_SPACER_LENGTH;
		}

		// Calculate offset and size for source code section:
		ushort sourceCodeOffset = 0;
		ushort sourceCodeSize = 0;
		bool hasSourceCode = ShaderData.CheckHasSourceCode(_shaderData.FileHeader, _shaderData.Description);
		if (hasSourceCode)
		{
			sourceCodeOffset = (ushort)(descriptionOffset + descriptionSize);
			if (_addSectionSpacers)
			{
				descriptionOffset += (ushort)SECTION_SPACER_LENGTH;
			}
			foreach (var kvp in _shaderData.SourceCode!)
			{
				sourceCodeSize += (ushort)kvp.Value.Length;
			}
		}

		// Calculate offset and size for compiled data section:
		byte compiledDataBlockCount = 0;
		uint compiledDataOffset = 0;
		uint compiledDataSize = 0;
		if (ShaderData.CheckHasCompiledData(_shaderData.FileHeader, _shaderData.Description))
		{
			compiledDataOffset = hasSourceCode
				? (uint)(sourceCodeOffset + sourceCodeSize)
				: (uint)(descriptionOffset + descriptionSize);
			if (_addSectionSpacers)
			{
				compiledDataOffset += (ushort)SECTION_SPACER_LENGTH;
			}
			AddByteCode(_shaderData.ByteCodeDxbc);
			AddByteCode(_shaderData.ByteCodeDxil);
			AddByteCode(_shaderData.ByteCodeSpirv);
			AddByteCode(_shaderData.ByteCodeMetal);

			void AddByteCode(byte[]? _byteCode)
			{
				if (_byteCode is not null && _byteCode.Length != 0)
				{
					compiledDataBlockCount++;
					compiledDataSize += (uint)(_byteCode.Length);
				}
			}
		}

		// Assemble and write header section:
		ShaderDataHeader header = new()
		{
			FileVersion = ShaderDataHeader.Version.Current,
			HeaderSize = headerSize,

			JsonOffset = descriptionOffset,
			JsonSize = descriptionSize,

			SourceCodeOffset = sourceCodeOffset,
			SourceCodeSize = sourceCodeSize,

			CompiledDataBlockCount = compiledDataBlockCount,
			CompiledDataOffset = compiledDataOffset,
			CompiledDataSize = compiledDataSize,
		};

		bool success = header.WriteFshaHeader(in _importCtx, _writer);
		return success;
	}

	#endregion
}
