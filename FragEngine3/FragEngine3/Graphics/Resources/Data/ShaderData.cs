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

	public ShaderDataFileHeader FileHeader { get; init; }

	// SOURCE CODE:

	public ShaderSourceCodeData? SourceCode { get; init; } = null;

	// COMPILED BYTE CODE:

	public byte[]? ByteCodeDxbc { get; init; } = null;
	public byte[]? ByteCodeDxil { get; init; } = null;
	public byte[]? ByteCodeSpirv { get; init; } = null;

	#endregion
	#region Methods

	public static bool Read(BinaryReader _reader, out ShaderData? _outData)
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

		//TODO: Read and deserialize JSON

		// Try reading source code data, if available:
		ShaderSourceCodeData? sourceCodeData = null;
		if (fileHeader.sourceCodeOffset != 0 && !ShaderSourceCodeData.Read(_reader, out sourceCodeData))
		{
			_outData = null;
			return false;
		}

		//TODO: Read compiled shader data.

		_outData = new()
		{
			FileHeader = fileHeader,
			//TODO: Assign description read from JSON.
			SourceCode = sourceCodeData,
			//TODO: Assign compiled shader data.
		};
		return true;
	}

	public bool Write(BinaryWriter _writer, bool _bundleSourceCode = false)
	{
		if (_writer is null)
		{
			Logger.Instance?.LogError("Cannot write shader data using null binary writer!");
			return false;
		}

		//TODO: Serialize description to JSON.
		uint jsonByteSize = 0;  //TEMP

		bool hasSourceCode = _bundleSourceCode && SourceCode is not null && !SourceCode.IsEmpty();

		if (!RecalculateOffsetsAndSizes(jsonByteSize, hasSourceCode))
		{
			return false;
		}

		// Write file header:
		if (!FileHeader.Write(_writer))
		{
			return false;
		}

		//TODO: Write description JSON.

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

	private bool RecalculateOffsetsAndSizes(uint _jsonByteSize, bool _hasSourceCode)
	{
		//TODO 1: Pre-calculate all sizes and offsets.
		//TODO 2: Update sizes and offsets in header.

		return true;
	}

	#endregion
}
