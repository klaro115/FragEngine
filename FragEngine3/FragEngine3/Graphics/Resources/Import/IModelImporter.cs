using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Interface for all importer types that can read and process 3D model data from file.
/// </summary>
public interface IModelImporter : IGraphicsResourceImporter
{
	#region Properties

	/// <summary>
	/// Gets flags of all vertex data types that this importer is capable of reading from file.
	/// </summary>
	MeshVertexDataFlags SupportedVertexData { get; }
	/// <summary>
	/// Gets whether this importer can read animations from a model file.
	/// </summary>
	bool CanImportAnimations { get; }
	/// <summary>
	/// Gets whether this importer can read and parse materials from a model file.
	/// </summary>
	bool CanImportMaterials { get; }
	/// <summary>
	/// Gets whether this importer can read and parse bundled texture assets from a model file.
	/// </summary>
	bool CanImportTextures { get; }

	#endregion
	#region Methods

	bool ImportSurfaceData(in ImporterContext _importCtx, Stream _resourceFileStream, out MeshSurfaceData? _outSurfaceData, string? _fileExtension = null);

	#endregion
}
