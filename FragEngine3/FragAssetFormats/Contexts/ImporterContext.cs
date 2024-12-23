using FragAssetFormats.Shaders.ShaderTypes;
using System.Text.Json;

namespace FragAssetFormats.Contexts;

public sealed class ImporterContext
{
	#region Properties

	public required ILogger Logger { get; init; }

	public required JsonSerializerOptions? JsonOptions { get; init; }

	public ShaderLanguage SupportedShaderLanguages { get; init; } = ShaderLanguage.ALL;
	public CompiledShaderDataType SupportedShaderDataTypes { get; init; } = CompiledShaderDataType.ALL;

	#endregion
}
