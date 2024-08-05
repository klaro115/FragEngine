namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Flags]
public enum CompiledShaderDataType
{
	DXBC	= 1,
	DXIL	= 2,
	SPIRV	= 4,
	//...

	ALL		= DXBC | DXIL | SPIRV,
}
