using FragAssetFormats.Contexts;
using FragAssetFormats.Shaders;
using FragAssetFormats.Shaders.FSHA;
using FragAssetFormats.Shaders.ShaderTypes;
using FragEngine3.EngineCore;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats.Internal;

internal static class ShaderFshaImporter
{
    #region Methods

    public static bool ImportShaderData(Stream _stream, ResourceHandle _resHandle, ResourceFileHandle _fileHandle, out ShaderData? _outShaderData)
    {
        EnginePlatformFlag platformFlags = _resHandle.resourceManager.engine.PlatformSystem.PlatformFlags;

        CompiledShaderDataType typeFlags = ShaderDataUtility.GetCompiledDataTypeFlagsForPlatform(platformFlags);
        ShaderLanguage language = ShaderDataUtility.GetShaderLanguageForPlatform(platformFlags);

        // Read the relevant shader data from stream:
        using BinaryReader reader = new(_stream);

        ImporterContext importCtx = new()
        {
            Logger = Logger.Instance!,
            JsonOptions = null,
            SupportedShaderLanguages = language,
            SupportedShaderDataTypes = typeFlags,
        };

        bool success = FshaImporter.ImportFromFSHA(in importCtx, reader, out _outShaderData);
        return success;
    }

    #endregion
}
