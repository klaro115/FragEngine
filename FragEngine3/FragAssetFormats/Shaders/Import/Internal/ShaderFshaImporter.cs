using FragAssetFormats.Shaders.ShaderTypes;
using FragEngine3.EngineCore;
using FragEngine3.Resources;

namespace FragAssetFormats.Shaders.Import.Internal;

internal static class ShaderFshaImporter
{
	#region Methods

	public static bool ImportShaderData(Stream _stream, ResourceHandle _resHandle, ResourceFileHandle _fileHandle, out ShaderData? _outShaderData)
	{
		EnginePlatformFlag platformFlags = _resHandle.resourceManager.engine.PlatformSystem.PlatformFlags;

		CompiledShaderDataType typeFlags = ShaderDataUtility.GetCompiledDataTypeFlagsForPlatform(platformFlags);

		// Read the relevant shader data from stream:
		using BinaryReader reader = new(_stream);

		return ShaderData.Read(reader, out _outShaderData, typeFlags);
	}

	#endregion
}
