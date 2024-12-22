using FragAssetFormats.Contexts;
using System.Text.Json;
using Veldrid;

namespace FragAssetFormats.Shaders.ShaderTypes;

[Serializable]
public sealed class ShaderDataDescription
{
	#region Properties

	public required ShaderStages Stage { get; init; }

	public required string MinCapabilities { get; init; }
	public required string MaxCapabilities { get; init; }

	public ShaderDataSourceCodeDesc[]? SourceCode { get; init; } = null;
	public ShaderDataCompiledBlockDesc[]? CompiledBlocks { get; init; } = null;

	#endregion
	#region Methods

	public static bool Read(in ImporterContext _importCtx, in ShaderDataHeader _header, BinaryReader _reader, out ShaderDataDescription? _outDescription)
	{
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data JSON description using null binary reader!");
			_outDescription = null;
			return false;
		}

		byte[] utf8JsonBytes = new byte[_header.JsonSize];
		try
		{
			int actualJsonLength = _reader.Read(utf8JsonBytes, 0, utf8JsonBytes.Length);
			MemoryStream utf8JsonStream = new(utf8JsonBytes, 0, actualJsonLength, false);

			_outDescription = JsonSerializer.Deserialize<ShaderDataDescription>(utf8JsonStream, _importCtx.JsonOptions);
			return _outDescription is not null;
		}
		catch (Exception ex)
		{
			_importCtx.Logger.LogException("Failed to deserialize shader data description from JSON!", ex);
			_outDescription = null;
			return false;
		}
	}

	public bool Write(in ImporterContext _importCtx, BinaryWriter _writer, out byte[] _outUtf8JsonBytes)
	{
		if (_writer is null)
		{
			_importCtx.Logger.LogError("Cannot write shader data JSON description using null binary writer!");
			_outUtf8JsonBytes = null!;
			return false;
		}

		try
		{
			_outUtf8JsonBytes = JsonSerializer.SerializeToUtf8Bytes(this, _importCtx.JsonOptions);
			return true;
		}
		catch (Exception ex)
		{
			_importCtx.Logger.LogException("Failed to serialize shader data description to JSON!", ex);
			_outUtf8JsonBytes = null!;
			return false;
		}
	}

	#endregion
}
