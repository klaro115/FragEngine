namespace FragEngine3.Graphics.Resources.Shaders.Internal;

[Serializable]
public readonly struct ShaderDataCompiledBlockDesc(CompiledShaderDataType _dataType, MeshVertexDataFlags _variantFlags, string _capabilities, uint _offset, uint _size, string _entryPoint)
{
	#region Fields

	public CompiledShaderDataType DataType { get; init; } = _dataType;
	public MeshVertexDataFlags VariantFlags { get; init; } = _variantFlags;
	public string Capabilities { get; init; } = _capabilities ?? string.Empty;
	public uint Offset { get; init; } = _offset;
	public uint Size { get; init; } = _size;
	public string EntryPoint { get; init; } = _entryPoint;

	#endregion
}
