using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

public static class ShaderDataUtility
{
	#region Methods

	public static bool GetGraphicsBackendForPlatform(EnginePlatformFlag _platformFlags, out GraphicsBackend _outBackend)
	{
		const EnginePlatformFlag vulkanPlatforms = EnginePlatformFlag.OS_Windows | EnginePlatformFlag.OS_Linux | EnginePlatformFlag.OS_FreeBSD;
		const EnginePlatformFlag d3dFlags = EnginePlatformFlag.OS_Windows | EnginePlatformFlag.GraphicsAPI_D3D;
		const EnginePlatformFlag metalFlags = EnginePlatformFlag.OS_MacOS | EnginePlatformFlag.GraphicsAPI_Metal;

		if ((_platformFlags & vulkanPlatforms) != 0 && _platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Vulkan))
		{
			_outBackend = GraphicsBackend.Vulkan;
			return true;
		}
		if (_platformFlags.HasFlag(d3dFlags))
		{
			_outBackend = GraphicsBackend.Direct3D11;
			return true;
		}
		if (_platformFlags.HasFlag(metalFlags))
		{
			_outBackend = GraphicsBackend.Metal;
			return true;
		}

		_outBackend = GraphicsBackend.OpenGL;
		return false;
	}

	public static CompiledShaderDataType GetCompiledDataTypeFlagsForPlatform(EnginePlatformFlag _platformFlags)
	{
		if (!GetGraphicsBackendForPlatform(_platformFlags, out GraphicsBackend backend))
		{
			return CompiledShaderDataType.Other;
		}

		return backend switch
		{
			GraphicsBackend.Direct3D11 => CompiledShaderDataType.DXBC | CompiledShaderDataType.DXIL,
			GraphicsBackend.Vulkan => CompiledShaderDataType.SPIRV,
			GraphicsBackend.Metal => CompiledShaderDataType.MetalArchive,
			_ => 0,
		};
	}

	public static ShaderLanguage GetShaderLanguageForPlatform(EnginePlatformFlag _platformFlags)
	{
		if (!GetGraphicsBackendForPlatform(_platformFlags, out GraphicsBackend backend))
		{
			return ShaderLanguage.GLSL;
		}

		return backend switch
		{
			GraphicsBackend.Direct3D11 => ShaderLanguage.HLSL,
			GraphicsBackend.Vulkan => ShaderLanguage.HLSL,
			GraphicsBackend.Metal => ShaderLanguage.Metal,
			_ => ShaderLanguage.GLSL,
		};
	}

	#endregion
}
