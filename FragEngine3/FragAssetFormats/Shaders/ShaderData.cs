using FragAssetFormats.Contexts;
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
	#region Methods

	public static bool Read(in ImporterContext _importCtx, BinaryReader _reader, out ShaderData _outShaderData)
	{
		if (_importCtx is null)
		{
			Console.WriteLine("Error! Cannot read shader data using null import context!");
			_outShaderData = null!;
			return false;
		}
		if (_reader is null)
		{
			_importCtx.Logger.LogError("Cannot read shader data from null binary reader!");
			_outShaderData = null!;
			return false;
		}
		if (!_reader.BaseStream.CanRead)
		{
			_importCtx.Logger.LogError("Cannot read shader data from write-only stream!");
			_outShaderData = null!;
			return false;
		}

		// Read header:
		if (!ShaderDataHeader.Read(in _importCtx, _reader, out ShaderDataHeader header))
		{
			_outShaderData = null!;
			return false;
		}

		// Read description JSON:
		if (!ShaderDataDescription.Read(in _importCtx, in header, _reader, out ShaderDataDescription? description) || description is null)
		{
			_outShaderData = null!;
			return false;
		}


		//TODO


		_outShaderData = new()
		{
			FileHeader = header,
			Description = description,
			//...
		};
		return true;
	}

	public bool Write(in ImporterContext _importCtx, BinaryWriter _writer)
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

		// Write header:
		if (!FileHeader.Write(in _importCtx, _writer))
		{
			return false;
		}

		// Write description JSON:
		if (!Description.Write(in _importCtx, _writer, out byte[] descriptionJsonBytes))
		{
			return false;
		}


		//TODO


		return true;
	}

	#endregion
}
