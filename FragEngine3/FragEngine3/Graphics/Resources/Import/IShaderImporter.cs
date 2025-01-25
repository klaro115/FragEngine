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
	ShaderLanguage SupportedSourceCodeLanguages { get; }
	/// <summary>
	/// Gets flags of all pre-compiled types of shader data that this importer can import directly.
	/// </summary>
	CompiledShaderDataType SupportedCompiledDataTypes { get; }

	#endregion
	#region Methods
	
	/// <summary>
	/// Tries to import shader data from a resource file stream.
	/// </summary>
	/// <param name="_importCtx">A context object providing instructions on how to import data, and which data to discard.</param>
	/// <param name="_resourceFileStream">A resource stream, typically a file stream, from which to read the shader data.</param>
	/// <param name="_outShaderData">Outputs a new instance of a shader data object from which a <see cref="ShaderResource"/> may be created.
	/// Null or invalid if the import fails.</param>
	/// <returns>True if the shader data was imported successfully, false otherwise.</returns>
	bool ImportShaderData(in ImporterContext _importCtx, Stream _resourceFileStream, out ShaderData? _outShaderData);

	#endregion
}
