using FragEngine3.EngineCore;
using FragEngine3.Utility.Serialization;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Serializable]
public sealed class ShaderDescriptionData
{
	#region Types

	[Serializable]
	public sealed class VariantData
	{
		public string? EntryPointNameBase { get; init; } = null;
		public MeshVertexDataFlags[]? VariantVertexFlags { get; init; } =
		[
			MeshVertexDataFlags.BasicSurfaceData
		];

		public bool IsValid() => !string.IsNullOrEmpty(EntryPointNameBase) || (VariantVertexFlags is not null && VariantVertexFlags.Length > 0);
	}

	[Serializable]
	public struct CompiledShaderData
	{
		public CompiledShaderDataType type;
		public uint byteSize;

		public readonly bool IsValid() => byteSize != 0;
	}

	#endregion
	#region Fields

	public static readonly ShaderDescriptionData none = new()
	{
		ShaderStage = ShaderStages.None,
		Variants = new()
		{
			EntryPointNameBase = null,
			VariantVertexFlags = null,
		},
		CompiledShaders = [],
	};

	#endregion
	#region Properties

	public ShaderStages ShaderStage { get; init; } = ShaderStages.None;

	public VariantData Variants { get; init; } = new();
	public CompiledShaderData[] CompiledShaders {  get; init; } = [];

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool isValid =
			ShaderStage != ShaderStages.None &&
			Variants is not null &&
			Variants.IsValid() &&
			CompiledShaders is not null &&
			CompiledShaders!.Length >= 1 &&
			CompiledShaders![0].IsValid();
		return isValid;
	}

	/// <summary>
	/// Calculates the byte offset from start of file, where compiled shader data of a specific type starts.
	/// </summary>
	/// <param name="_type">The type of compiled shader data we're looking for.</param>
	/// <param name="_shaderDataOffset"></param>
	/// <param name="_outByteOffset">Outputs the byte offset where the compiled data starts, or zero, if the type wasn't found.</param>
	/// <param name="_outByteSize">Outputs the byte size of the compiled data, or zero, if the type wasn't found.</param>
	/// <returns>True if compiled data of the given type exists within this file, false otherwise.</returns>
	public bool GetCompiledShaderByteOffsetAndSize(CompiledShaderDataType _type, uint _shaderDataOffset, out uint _outByteOffset, out uint _outByteSize)
	{
		if (CompiledShaders is not null)
		{
			_outByteOffset = _shaderDataOffset;
			for (int i = 0; i < CompiledShaders.Length; i++)
			{
				if (CompiledShaders[i].type == _type)
				{
					_outByteSize = CompiledShaders[i].byteSize;
					return true;
				}
				_outByteOffset += CompiledShaders[i].byteSize;
			}
		}
		_outByteOffset = 0u;
		_outByteSize = 0u;
		return false;
	}

	public static bool Read(BinaryReader _reader, uint _jsonByteLength, out ShaderDescriptionData _outDesc)
	{
		if (_reader is null || _jsonByteLength == 0)
		{
			_outDesc = none;
			return false;
		}

		string jsonTxt;
		try
		{
			byte[] utf8Bytes = _reader.ReadBytes((int)_jsonByteLength);
			jsonTxt = Encoding.UTF8.GetString(utf8Bytes);
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read shader description JSON from stream!", ex);
			_outDesc = none;
			return false;
		}

		if (!DeserializeFromJson(jsonTxt, out _outDesc!))
		{
			_outDesc = none;
			return false;
		}
		return _outDesc.IsValid();
	}

	public bool Write(out byte[] _outJsonUtf8Bytes)
	{
		if (!IsValid())
		{
			_outJsonUtf8Bytes = [];
			return false;
		}

		if (!SerializeToJson(out string jsonTxt))
		{
			_outJsonUtf8Bytes = [];
			return false;
		}

		try
		{
			_outJsonUtf8Bytes = Encoding.UTF8.GetBytes(jsonTxt);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to convert shader description JSON to UTF-8 bytes!", ex);
			_outJsonUtf8Bytes = [];
			return false;
		}
	}

	public static bool DeserializeFromJson(string _jsonTxt, out ShaderDescriptionData? _outDesc)
	{
		if (string.IsNullOrEmpty(_jsonTxt))
		{
			_outDesc = none;
			return false;
		}

		return Serializer.DeserializeFromJson(_jsonTxt, out _outDesc) && _outDesc!.IsValid();
	}

	public bool SerializeToJson(out string _outJsonTxt)
	{
		if (!IsValid())
		{
			_outJsonTxt = string.Empty;
			return false;
		}

		return Serializer.SerializeToJson(this, out  _outJsonTxt);
	}

	#endregion
}
