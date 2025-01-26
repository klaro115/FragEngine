using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Graphics.Resources.Shaders;

namespace FragEngine3.Graphics.Resources.Export;

/// <summary>
/// Interface of all exporter types that can export and serialize shader data to some resources file format.
/// </summary>
public interface IShaderExporter : IGraphicsResourceImporter
{
	#region Properties

	/// <summary>
	/// Gets flags of all supported shader languages in which source code files can be created, or that can be included in the export.
	/// </summary>
	ShaderLanguage SupportedSourceCodeLanguages { get; }
	/// <summary>
	/// Gets flags of all compiled types of shader data that this exporter can process and include in the export.
	/// </summary>
	CompiledShaderDataType SupportedCompiledDataTypes { get; }

	#endregion
	#region Methods

	bool ExportShaderData(in ImporterContext _exportCtx, ShaderData _shaderData, Stream _outputResourceStream);

	#endregion
}
