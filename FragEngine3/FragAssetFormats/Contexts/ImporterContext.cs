using FragAssetFormats.Shaders.ShaderTypes;
using System.Text.Json;

namespace FragAssetFormats.Contexts;

/// <summary>
/// Context object for importing and exporting resource data.
/// This object contains flags and instructions on which data to include or exclude from the imported/exported files.
/// </summary>
public sealed class ImporterContext
{
	#region Properties

	/// <summary>
	/// A logger instance to use for outputting error and warning messages during import or export.
	/// </summary>
	public required ILogger Logger { get; init; }

	/// <summary>
	/// Options for JSON serialization. If null, default options will be used.
	/// </summary>
	public required JsonSerializerOptions? JsonOptions { get; init; }

	/// <summary>
	/// Flags of all shader languages that are supported. For import, only source code in these languages is loaded.
	/// For export, only source code in these languages is bundled with exported shader assets.
	/// </summary>
	public ShaderLanguage SupportedShaderLanguages { get; init; } = ShaderLanguage.ALL;
	/// <summary>
	/// Flags of all types of compiled shader data that are supported. For import, only compiled data of these types is loaded.
	/// For export, only compiled data of these types is bundled with exported shader assets.
	/// </summary>
	public CompiledShaderDataType SupportedShaderDataTypes { get; init; } = CompiledShaderDataType.ALL;

	#endregion
}
