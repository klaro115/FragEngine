using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.ShaderGen;
using FragEngine3.Utility.Serialization;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Serializable]
public sealed class ShaderDescriptionVariantData
{
	#region Properties

	/// <summary>
	/// The type of compiled shader data. Basically, what backend/runtime is was compiled for.
	/// </summary>
	public CompiledShaderDataType Type { get; init; }

	/// <summary>
	/// Vertex data flags of this variant.
	/// </summary>
	public MeshVertexDataFlags VariantFlags { get; init; } = 0;
	/// <summary>
	/// String-encoded description of variant features and flags. May be decoded to <see cref="ShaderGen.ShaderGenConfig"/>.
	/// </summary>
	public string VariantDescriptionTxt { get; init; } = string.Empty;
	/// <summary>
	/// Name of this variant's entrypoint function within the source code.
	/// </summary>
	public string EntryPoint { get; init; } = string.Empty;

	/// <summary>
	/// Starting position offset relative to beginning of compiled shader data section.
	/// </summary>
	public uint ByteOffset { get; set; }
	/// <summary>
	/// Size of the compiled data block, in bytes.
	/// </summary>
	public uint ByteSize { get; init; }

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool result =
			Type != CompiledShaderDataType.Other &&
			VariantFlags != 0 &&
			ByteSize != 0 &&
			!string.IsNullOrEmpty(VariantDescriptionTxt);
		return result;
	}

	#endregion
}

[Serializable]
public sealed class ShaderDescriptionSourceCodeData
{
	#region Fields

	[Serializable]
	public sealed class VariantEntryPoint
	{
		public MeshVertexDataFlags VariantFlags { get; init; } = MeshVertexDataFlags.BasicSurfaceData;
		public string EntryPoint { get; init; } = string.Empty;
	}

	[Serializable]
	public sealed class SourceCodeBlock
	{
		public ShaderGenLanguage Language { get; init; } = 0;
		public uint ByteOffset { get; set; } = 0;
		public uint ByteSize { get; init; } = 0;
	}

	#endregion
	#region Properties

	// ENTRY POINTS:

	/// <summary>
	/// Name base of all entry point functions within the source code for this stage.
	/// Suffixes added to this name may be used to find and identify variants during run-time compilation.
	/// </summary>
	public string EntryPointNameBase { get; init; } = string.Empty;
	/// <summary>
	/// An array of all variant entry points and their respective vertex data flags. If null, entry points will be scanned and identified
	/// based on '<see cref="EntryPointNameBase"/>' and standard variant suffixes instead.
	/// </summary>
	public VariantEntryPoint[]? EntryPoints { get; init; } = null;

	// FEATURES:

	/// <summary>
	/// String-encoded list of all ShaderGen features supported by this shader's source code. (i.e. what is possible)
	/// </summary>
	public string SupportedFeaturesTxt { get; init; } = string.Empty;
	/// <summary>
	/// String-encoded list of all ShaderGen features that were enabled for the most fully-featured variant. (i.e. what has been pre-compiled)
	/// </summary>
	public string MaximumCompiledFeaturesTxt { get; init; } = string.Empty;

	// CODE BLOCKS:

	/// <summary>
	/// An array of all source code blocks bundled with this file, each in a different language.
	/// </summary>
	public SourceCodeBlock[] SourceCodeBlocks { get; init; } = [];

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool result =
			!string.IsNullOrEmpty(EntryPointNameBase) &&
			!string.IsNullOrEmpty(SupportedFeaturesTxt) &&
			!string.IsNullOrEmpty(MaximumCompiledFeaturesTxt) &&
			SourceCodeBlocks is not null &&
			SourceCodeBlocks.Length != 0;
		return result;
	}

	#endregion
}

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
