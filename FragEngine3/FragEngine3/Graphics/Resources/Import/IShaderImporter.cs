using FragEngine3.Graphics.Resources.Shaders;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Interface of all importer types that can import shader programs either from source code, from pre-compiled data, or from some bundled format.
/// </summary>
public interface IShaderImporter : IGraphicsResourceImporter
{
	#region Properties

	/// <summary>
	/// Gets flags of all supported shader languages from which source code files can be imported directly.
	/// </summary>
	public ShaderLanguage SupportedSourceCodeLanguages { get; }
	/// <summary>
	/// Gets flags of all pre-compiled types of shader data that this importer can import directly.
	/// </summary>
	public CompiledShaderDataType SupportedCompiledDataTypes { get; }

	#endregion
	#region Methods

	bool ImportShaderData(in ImporterContext _importCtx, Stream _resourceFileStream, out ShaderData? _outShaderData);

	#endregion
}
