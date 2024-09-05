using FragEngine3.EngineCore;
using FragEngine3.Utility.Serialization;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Serializable]
public sealed class ShaderDescriptionData
{
	#region Fields

	public static readonly ShaderDescriptionData none = new()
	{
		ShaderStage = ShaderStages.None,
		SourceCode = null,
		CompiledVariants = [],
	};

	#endregion
	#region Properties

	/// <summary>
	/// Which stage of the shader pipeline this file's shader programs belong to.
	/// </summary>
	public ShaderStages ShaderStage { get; init; } = ShaderStages.None;
	/// <summary>
	/// Which vertex data variants should be compiled and prepared for run-time use, assuming they are defined.
	/// If a variant does not exist in either source code or pre-compiled variants, it will be skipped during import.
	/// Similarly, if a variant has flags other than these, it will be skipped during import.
	/// </summary>
	public MeshVertexDataFlags RequiredVariants { get; init; } = MeshVertexDataFlags.ALL;

	/// <summary>
	/// Optional description of source code data bundled within this file.
	/// If source code is provided, source code may be used to compile missing shader variants on-demand.
	/// </summary>
	public ShaderDescriptionSourceCodeData? SourceCode { get; init; } = null;
	/// <summary>
	/// An array of all pre-compiled shader variants contained within this file.<para/>
	/// Variants in this array must be sorted in ascending order by type, then vertex data flags, and lastly feature-completeness.
	/// </summary>
	public ShaderDescriptionVariantData[] CompiledVariants { get; init; } = [];

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool isValid =
			ShaderStage != ShaderStages.None &&
			(SourceCode is null || SourceCode.IsValid()) &&
			CompiledVariants is not null &&
			CompiledVariants.Length != 0 &&
			CompiledVariants[0].IsValid();
		return isValid;
	}

	/// <summary>
	/// Calculates the byte offset from start of file, where compiled shader data of a specific type starts.
	/// </summary>
	/// <param name="_type">The type of compiled shader data we're looking for.</param>
	/// <param name="_variantFlags">Vertex data flags of the shader variant we're looking for.</param>
	/// <param name="_variantDescriptionTxt">A string-encoded description of the feature set we're looking for. If null, this is ignored. If provided, this is prioritized.
	/// This description's format corresponds to the output of <see cref="ShaderGen.ShaderGenConfig.CreateDescriptionTxt"/>, and it is case-sensitive.</param>
	/// <param name="_outVariantData">Outputs description data for the most first fitting variant that is present in pre-compiled form.</param>
	/// <returns>True if compiled data of the given type exists within this file, false otherwise.</returns>
	public bool GetCompiledShaderVariantData(CompiledShaderDataType _type, MeshVertexDataFlags _variantFlags, string? _variantDescriptionTxt, out ShaderDescriptionVariantData _outVariantData)
	{
		if (CompiledVariants is null || _variantFlags == 0)
		{
			_outVariantData = null!;
			return false;
		}

		// Prioritize matching the exact ShaderGen variant description, if provided:
		if (string.IsNullOrEmpty(_variantDescriptionTxt))
		{
			foreach (ShaderDescriptionVariantData variant in CompiledVariants)
			{
				if (variant.Type == _type && variant.VariantDescriptionTxt == _variantDescriptionTxt)
				{
					_outVariantData = variant;
					return true;
				}
			}
		}
		
		// Find the first compatible vertex variant based on vertex data flags: (lowest feature set is assumed to be listed first)
		foreach (ShaderDescriptionVariantData variant in CompiledVariants)
		{
			if (variant.Type == _type && variant.VariantFlags.HasFlag(_variantFlags))
			{
				_outVariantData = variant;
				return true;
			}
		}

		_outVariantData = null!;
		return false;
	}

	public static bool Read(BinaryReader _reader, uint _jsonByteLength, out ShaderDescriptionData _outDesc)
	{
		if (_reader is null || _jsonByteLength == 0)
		{
			_outDesc = none;
			return false;
		}

		if (!ShaderData.ReadUtf8Bytes(_reader, _jsonByteLength, out string jsonTxt))
		{
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
