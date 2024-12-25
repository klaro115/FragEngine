using FragAssetFormats.Shaders.ShaderTypes;

namespace FragAssetFormats.Shaders;

[Serializable]
public sealed class ShaderData
{
	#region Properties

	// GENERAL:

	public required ShaderDataHeader FileHeader { get; set; }
	public required ShaderDataDescription Description { get; init; }

	// SOURCE CODE:

	public Dictionary<ShaderLanguage, byte[]>? SourceCode = null;

	// COMPILED BYTE CODE:

	public byte[]? ByteCodeDxbc { get; init; } = null;
	public byte[]? ByteCodeDxil { get; init; } = null;
	public byte[]? ByteCodeSpirv { get; init; } = null;
	public byte[]? ByteCodeMetal { get; init; } = null;
	//...

	#endregion
	#region Methods General

	public static bool CheckHasSourceCode(in ShaderDataHeader _header, in ShaderDataDescription? _description)
	{
		if (_header is null)
		{
			return false;
		}
		if (_header.SourceCodeOffset == 0 || _header.SourceCodeSize == 0)
		{
			return false;
		}
		return _description is null || (_description.SourceCode is not null && _description.SourceCode.Length != 0);
	}

	public static bool CheckHasCompiledData(in ShaderDataHeader _header, in ShaderDataDescription? _description)
	{
		if (_header is null)
		{
			return false;
		}
		if (_header.CompiledDataBlockCount == 0 || _header.CompiledDataOffset == 0 || _header.CompiledDataSize == 0)
		{
			return false;
		}
		return _description is null || (_description.CompiledBlocks is not null && _description.CompiledBlocks.Length != 0);
	}

	#endregion
}
