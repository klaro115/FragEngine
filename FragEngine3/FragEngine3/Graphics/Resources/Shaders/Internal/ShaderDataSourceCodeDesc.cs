namespace FragEngine3.Graphics.Resources.Shaders.Internal;

[Serializable]
public readonly struct ShaderDataSourceCodeDesc(ShaderLanguage _language, MeshVertexDataFlags _variantFlags, ushort _offset, ushort _size, string _entryPoint)
{
	#region Fields

	public readonly ShaderLanguage language = _language;
	public readonly MeshVertexDataFlags variantFlags = _variantFlags;
	public readonly ushort offset = _offset;
	public readonly ushort size = _size;
	public readonly string entryPoint = _entryPoint;

	#endregion
}
