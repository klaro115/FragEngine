using FragEngine3.Graphics.Resources;

namespace FragAssetFormats.Shaders.ShaderTypes;

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
	/// String-encoded description of variant features and flags. May be decoded to <see cref="Resources.ShaderConfig"/>.
	/// </summary>
	public string VariantDescriptionTxt { get; init; } = string.Empty;
	/// <summary>
	/// Name of this variant's entrypoint function within the source code.
	/// </summary>
	public string EntryPoint { get; init; } = string.Empty;

	/// <summary>
	/// Starting position offset relative to beginning of its data type's block in compiled shader data section.
	/// </summary>
	public uint RelativeByteOffset { get; set; }
	/// <summary>
	/// Starting position offset relative to beginning of compiled shader data section.
	/// </summary>
	public uint TotalByteOffset { get; set; }
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
