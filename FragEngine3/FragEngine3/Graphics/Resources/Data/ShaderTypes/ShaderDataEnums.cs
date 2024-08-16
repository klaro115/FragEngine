namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Flags]
public enum CompiledShaderDataType : byte
{
	DXBC			= 1,
	DXIL			= 2,
	SPIRV			= 4,
	MetalArchive	= 8,

	Other			= 128,

	ALL				= DXBC | DXIL | SPIRV | MetalArchive
}
