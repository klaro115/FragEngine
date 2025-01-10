namespace FragEngine3.Graphics.Resources.Shaders.Internal;

[Serializable]
public readonly struct ShaderDataCompiledBlockDesc(CompiledShaderDataType _dataType, MeshVertexDataFlags _variantFlags, string _capabilities, uint _offset, uint _size)
{
	#region Fields

	public readonly CompiledShaderDataType dataType = _dataType;
	public readonly MeshVertexDataFlags variantFlags = _variantFlags;
	public readonly string capabilities = _capabilities ?? string.Empty;
	public readonly uint offset = _offset;
	public readonly uint size = _size;

	#endregion
}
