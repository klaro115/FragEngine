using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Graphics.Resources.Shaders.Internal;

namespace FragAssetFormats.Shaders;

/// <summary>
/// Serializable object representing a shader resource/asset.
/// This type can contain both source code and pre-compiled shader variants.
/// </summary>
[Serializable]
public sealed class ShaderData
{
	#region Properties

	// GENERAL:

	/// <summary>
	/// File header description with section sizes and offsets; only needed for FSHA import/export.
	/// </summary>
	public FshaFileHeader? FileHeader { get; set; } = null;
	public required ShaderDataDescription Description { get; init; }

	// SOURCE CODE:

	public Dictionary<ShaderLanguage, byte[]>? SourceCode { get; init; } = null;

	// COMPILED BYTE CODE:

	public byte[]? ByteCodeDxbc { get; init; } = null;		//TODO: Convert these into a dictionary as well. Working with 4 arrays is kind of messy and verbose.
	public byte[]? ByteCodeDxil { get; init; } = null;
	public byte[]? ByteCodeSpirv { get; init; } = null;
	public byte[]? ByteCodeMetal { get; init; } = null;
	//...

	#endregion
	#region Methods

	/// <summary>
	/// Checks whether a set of shader data contains usable source code definitions.
	/// </summary>
	/// <param name="_header">The FSHA header of the shader data instance.</param>
	/// <param name="_description">A description of the bundled shader data. If null, the check will be based on the header only.</param>
	/// <returns>True if source code information appears to be present and complete.</returns>
	public static bool CheckHasSourceCode(in FshaFileHeader? _header, in ShaderDataDescription? _description)
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

	/// <summary>
	/// Checks whether a set of shader data contains usable pre-compiled data definitions.
	/// </summary>
	/// <param name="_header">The FSHA header of the shader data instance.</param>
	/// <param name="_description">A description of the bundled shader data. If null, the check will be based on the header only.</param>
	/// <returns>True if pre-compiled data information appears to be present and complete.</returns>
	public static bool CheckHasCompiledData(in FshaFileHeader? _header, in ShaderDataDescription? _description)
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

	/// <summary>
	/// Tries to retrieve the byte code containing source code in a specific language.
	/// </summary>
	/// <param name="_language">The shading langugage that we want the source code to be in.</param>
	/// <param name="_outSourceCodeBytes">Outputs a byte array containing source code encoded as either ASCII or UTF-8 plaintext.
	/// Null if no source code in the requested language exists.</param>
	/// <returns>True if source code was found, false otherwise.</returns>
	public bool TryGetFullSourceCode(ShaderLanguage _language, out byte[]? _outSourceCodeBytes)
	{
		if (SourceCode is not null && SourceCode.TryGetValue(_language, out _outSourceCodeBytes))
		{
			return true;
		}
		_outSourceCodeBytes = null;
		return false;
	}

	/// <summary>
	/// Tries to retrieve a sub-section of the source code that pertains to a specific variant of the shader for a specific shading lamgauge.
	/// </summary>
	/// <param name="_language">The shading langugage that we want the source code to be in.</param>
	/// <param name="_variantFlags">Vertex data flags identifying the exact variant we are looking for.</param>
	/// <param name="_outVariantSourceCodeBytes">Outputs a region of memory within the source code byte data, that the requested variant's source code is defined in.</param>
	/// <param name="_outEntryPoint">Outputs the name of the variant's entry point function.</param>
	/// <returns>True if a variant in the requested language exists and was found, false otherwise.</returns>
	public bool TryGetVariantSourceCode(ShaderLanguage _language, MeshVertexDataFlags _variantFlags, out ReadOnlySpan<byte> _outVariantSourceCodeBytes, out string _outEntryPoint)
	{
		if (!TryGetFullSourceCode(_language, out byte[]? fullSourceCodeBytes))
		{
			_outVariantSourceCodeBytes = [];
			_outEntryPoint = string.Empty;
			return false;
		}
		if (!Description.TryGetVariantSourceCode(_language, _variantFlags, out ShaderDataSourceCodeDesc blockDesc))
		{
			_outVariantSourceCodeBytes = [];
			_outEntryPoint = string.Empty;
			return false;
		}

		_outVariantSourceCodeBytes = new(fullSourceCodeBytes, blockDesc.offset, blockDesc.size);
		_outEntryPoint = blockDesc.entryPoint;
		return true;
	}

	/// <summary>
	/// Tries to retrieve all byte code for a specific type of compiled shader data.
	/// </summary>
	/// <param name="_dataType">The type of compiled shader data we're looking for.</param>
	/// <param name="_outByteCode">Outputs a byte array with the requested compiled data. Null if no data of that type was found.</param>
	/// <returns>True if the shader data includes compiled data of the requested type, false otherwise.</returns>
	public bool TryGetByteCode(CompiledShaderDataType _dataType, out byte[]? _outByteCode)
	{
		_outByteCode = _dataType switch
		{
			CompiledShaderDataType.DXBC => ByteCodeDxbc,
			CompiledShaderDataType.DXIL => ByteCodeDxil,
			CompiledShaderDataType.SPIRV => ByteCodeSpirv,
			CompiledShaderDataType.MetalArchive => ByteCodeMetal,
			_ => null,
		};
		return _outByteCode is not null && _outByteCode.Length != 0;
	}

	/// <summary>
	/// Checks whether the data is valid and complete enough to use.
	/// </summary>
	/// <returns>True if the necessary members are non-null and look to have valid contents.</returns>
	public bool IsValid()
	{
		bool result =
			Description is not null &&
			Description.IsValid() &&
			((SourceCode is not null && SourceCode.Count != 0) ||
			IsArrayNotNullOrEmpty(ByteCodeDxbc) ||
			IsArrayNotNullOrEmpty(ByteCodeDxil) ||
			IsArrayNotNullOrEmpty(ByteCodeSpirv) ||
			IsArrayNotNullOrEmpty(ByteCodeMetal));
		return result;

		// Local helper method for checking if an array is non-null and has elements:
		static bool IsArrayNotNullOrEmpty(Array? _array)
		{
			return _array is not null && _array.Length > 0;
		}
	}

	#endregion
}
