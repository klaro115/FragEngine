using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Graphics.Resources;

namespace FragAssetPipeline.Resources.Shaders.FSHA;

internal sealed class FshaCompiledVariant
{
	public CompiledShaderDataType shaderType = CompiledShaderDataType.Other;
	public MeshVertexDataFlags vertexDataFlags = 0;
	public string entryPoint = string.Empty;
	public byte[] compiledData = [];
	public uint byteOffset = 0u;
	public uint totalByteOffset = 0u;
}
