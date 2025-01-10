using FragAssetFormats.Shaders;
using FragEngine3.Graphics.Resources.Import;
using System.Text.Json;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Shaders.Internal;

/// <summary>
/// A description of the contents and layout of a <see cref="ShaderData"/> object.
/// </summary>
[Serializable]
public sealed class ShaderDataDescription
{
	#region Properties

	/// <summary>
	/// The shader stage that this resource can be bound to.
	/// </summary>
	public required ShaderStages Stage { get; init; }

	/// <summary>
	/// Description string of the minimum capabilities of this shader, relative to the engine's standard shader feature suite.
	/// </summary>
	public required string MinCapabilities { get; init; }
	/// <summary>
	/// Description string of the maximum capabilities of this shader, relative to the engine's standard shader feature suite.
	/// </summary>
	public required string MaxCapabilities { get; init; }

	/// <summary>
	/// Optional array of descriptions of the different source code blocks bundled with this shader data.
	/// Each block may contain the shader's full source code in a different shader language, encoded as ASCII or UTF-8 plaintext.
	/// </summary>
	public ShaderDataSourceCodeDesc[]? SourceCode { get; init; } = null;
	/// <summary>
	/// Optional array of descriptions of the different compiled data blocks bundled with this shader data.
	/// Each block may contain a different pre-compiled variant of a shader program for a specific graphics backend.
	/// </summary>
	public ShaderDataCompiledBlockDesc[]? CompiledBlocks { get; init; } = null;

	#endregion
	#region Methods

	/// <summary>
	/// Checks whether the contents of this description object appear to be complete and ready for use.
	/// </summary>
	/// <returns>True if the description is valid, false otherwise.</returns>
	public bool IsValid()
	{
		bool result =
			Stage != ShaderStages.None &&
			!string.IsNullOrEmpty(MinCapabilities) &&
			!string.IsNullOrEmpty(MaxCapabilities) &&
			(SourceCode is not null || CompiledBlocks is not null);
		return result;
	}

	public static bool DeserializeFromJson(in ImporterContext _importCtx, uint _jsonByteSize, BinaryReader _reader, out ShaderDataDescription? _outDescription)
	{
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data JSON description using null binary reader!");
			_outDescription = null;
			return false;
		}

		byte[] utf8JsonBytes = new byte[_jsonByteSize];
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

	public bool SerializeToJson(in ImporterContext _importCtx, BinaryWriter _writer, out byte[] _outUtf8JsonBytes)
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
