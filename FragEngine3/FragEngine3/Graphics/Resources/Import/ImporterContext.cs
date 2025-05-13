using FragEngine3.EngineCore.Logging;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;
using System.Globalization;
using System.Text.Json;

namespace FragEngine3.Graphics.Resources.Import;

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
	/// A resource manager instance to use for registering additional sub-resources or auxillary files for dependency import.
	/// </summary>
	public IResourceManager? ResourceManager { get; init; }

	/// <summary>
	/// Options for JSON serialization. If null, default options will be used.
	/// </summary>
	public required JsonSerializerOptions? JsonOptions { get; init; }

	/// <summary>
	/// Whether to use human-readable, clean formatting and data layouts over more dense representations, if available.
	/// </summary>
	public bool PreferNiceFormatting { get; init; } = true;

	/// <summary>
	/// The culture to use for parsing text-based formats and encodings. English or invariant culture should be used if possible.
	/// </summary>
	public CultureInfo CultureInfo { get; init; } = CultureInfo.InvariantCulture;

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

	/// <summary>
	/// Whether to prefer bit depths of more than 8 bits per color channel per pixel for image and video import.
	/// </summary>
	public bool PreferHighBitDepth { get; init; } = true;
	/// <summary>
	/// Whether to prefer HDR images or videos over SDR.
	/// </summary>
	public bool PreferHDR { get; init; } = true;
	/// <summary>
	/// Whether to bake the offset or relative position of sub-resources to the resulting resource's data.<para/>
	/// Example: In 3D model import, the transformations of sub-meshes within a 3D scene will be be applied directly to the geometry.
	/// If false, resources created from sub-meshes will will be exported in the sub-mesh's local space, while sub-mesh hierarchy
	/// information and transformations are discarded.
	/// </summary>
	public bool BakeSubResourceOffsets { get; init; } = false;

	/// <summary>
	/// Flags of compression behaviours. This dictates whether compression is at all applied on export, and how aggressively data
	/// should be compressed and optimized.
	/// </summary>
	public CompressionBehaviourFlags CompressionBehaviour { get; init; } = CompressionBehaviourFlags.PreferCompression | CompressionBehaviourFlags.OptimizeDataTypes;
	/// <summary>
	/// Flags of all types of data that should be compressed when exporting a resource, or that you expect compression on when importing.
	/// </summary>
	public CompressedDataFlags CompressedDataTypes { get; init; } = CompressedDataFlags.ALL;

	#endregion
	#region Methods

	/// <summary>
	/// Checks whether all values and states of this context object are complete and valid.
	/// </summary>
	/// <returns>True if the context is ready to use, false otherwise.</returns>
	public bool IsValid()
	{
		bool result =
			Logger is not null &&
			Logger.IsInitialized &&
			CultureInfo is not null &&
			SupportedShaderLanguages != 0 &&
			SupportedShaderDataTypes != 0;
		return result;
	}

	#endregion
}
