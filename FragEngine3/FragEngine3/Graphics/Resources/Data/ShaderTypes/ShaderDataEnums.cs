namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Flags]
public enum CompiledShaderDataType : byte
{
	DXBC	= 1,
	DXIL	= 2,
	SPIRV	= 4,
	// TODO: Add Metal compiled shader formats (Metal archive and/or library)

	Other	= 128,

	ALL		= DXBC | DXIL | SPIRV, // | Metal
}
