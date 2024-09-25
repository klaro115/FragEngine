namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

/// <summary>
/// Enumeration of the different types of compiled shader data, i.e. what API or
/// backend a shader program is compiled for.
/// </summary>
[Flags]
public enum CompiledShaderDataType : byte
{
	/// <summary>
	/// Old-style Direct3D shader byte code. This format is equivalent to the
	/// output of 'D3dCompiler.h'.
	/// </summary>
	DXBC			= 1,
	/// <summary>
	/// Dx12-style Direct3D intermediate language, produced by the DXC.
	/// </summary>
	DXIL			= 2,
	/// <summary>
	/// Vulkan's portable intermediate shader code.
	/// </summary>
	SPIRV			= 4,
	/// <summary>
	/// Metal shader archive. This is a compiled MSL shader library.
	/// </summary>
	MetalArchive	= 8,

	/// <summary>
	/// Unknown or unsupported compiled shader format. Variants of this type are
	/// skipped on import.
	/// </summary>
	Other			= 128,

	ALL				= DXBC | DXIL | SPIRV | MetalArchive
}

/// <summary>
/// Enumeration of different high level shading languages. Note that most of these
/// languages may be specific to a single platform or graphics API.
/// </summary>
[Flags]
public enum ShaderLanguage
{
	/// <summary>
	/// Mircosoft's Direct3D shading language. This is the default language that
	/// will be favored by the engine's systems.
	/// </summary>
	HLSL = 1,
	/// <summary>
	/// Apple's Metal shading language (MSL), which is only supported by MacOS and
	/// iOS platforms running a Metal graphics backend.
	/// </summary>
	Metal = 2,
	/// <summary>
	/// OpenGL shading language, which is supported by multiple graphics backends
	/// to varying degrees, including but not limited to OpenGL, GLES, and Vulkan.
	/// If HLSL source code is not available, this may be used to compile shaders
	/// for Vulkan.
	/// </summary>
	GLSL = 4,

	/// <summary>
	/// All shader language flags are raised. This value should only be used by
	/// methods that actually expect flags rather than a singular language selection.
	/// </summary>
	ALL = HLSL | Metal | GLSL
}
