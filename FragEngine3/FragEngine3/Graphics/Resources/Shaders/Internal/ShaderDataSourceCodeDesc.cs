namespace FragEngine3.Graphics.Resources.Shaders.Internal;

/// <summary>
/// Structure describing a single block of source code data contained in the data of a shader file.
/// Each block contains the full plaintext source code of the shader program in a specific language.
/// The actual byte data that is referenced through this block is encoded as either ASCII or UTF-8.
/// </summary>
/// <param name="_language">The shader language in which the source code is written.</param>
/// <param name="_variantFlags">Vertex data flags for this variant.</param>
/// <param name="_offset">Byte offset between the beginning of the source code data and the start of
/// this block</param>
/// <param name="_size">The total size of this block, in bytes.</param>
/// <param name="_entryPoint">The name of the shader's entry point function.</param>
[Serializable]
public readonly struct ShaderDataSourceCodeDesc(ShaderLanguage _language, MeshVertexDataFlags _variantFlags, ushort _offset, ushort _size, string _entryPoint)
{
	#region Fields

	public ShaderLanguage Language { get; init; } = _language;
	public MeshVertexDataFlags VariantFlags { get; init; } = _variantFlags;
	public ushort Offset { get; init; } = _offset;
	public ushort Size { get; init; } = _size;
	public string EntryPoint { get; init; } = _entryPoint;

	/// <summary>
	/// Represents an empty and invalid block of source code.
	/// </summary>
	public static readonly ShaderDataSourceCodeDesc none = new(0, 0, 0, 0, string.Empty);

	#endregion
}
