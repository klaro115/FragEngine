using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Interface for all importer types that can read and process 3D model data from file.
/// </summary>
public interface IModelImporter : IGraphicsResourceImporter
{
	#region Properties

	// GEOMETRY SUPPORT:

	/// <summary>
	/// Gets flags of all vertex data types that this importer is capable of reading from file.
	/// </summary>
	MeshVertexDataFlags SupportedVertexData { get; }
	/// <summary>
	/// Gets whether the exporter can parse and output triangle indices in 16-bit format.<para/>
	/// Note: Meshes with 16-bit triangle indices can contain up to 65336 vertices.
	/// </summary>
	bool Supports16BitIndices { get; }
	/// <summary>
	/// Gets whether the exporter can parse and output triangle indices in 32-bit format.<para/>
	/// Note: Meshes with 32-bit triangle indices can contain up to 4.29 billion vertices.
	/// </summary>
	bool Supports32BitIndices { get; }
	/// <summary>
	/// Gets whether this importer can import sub-meshes from a model file. If false, all geometry in a file will always be combined into a single mesh.
	/// </summary>
	bool CanImportSubMeshes { get; }

	// ANIMATION SUPPORT:

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

	/// <summary>
	/// Tries to read and import polygonal surface geometry from a 3D model file.
	/// </summary>
	/// <param name="_importCtx">An object providing additional instructions and references for importing resources.</param>
	/// <param name="_resourceFileStream">A file stream from which to read the 3D model. The stream's read position must be at the start of the model file.</param>
	/// <param name="_resourceKey">The identifier name of the mesh or sub-mesh resource we want to import from the 3D model file.</param>
	/// <param name="_outSurfaceData">Outputs polygonal surface geometry data for one of or all of the file's 3D models.</param>
	/// <param name="_fileExtension">Optional. The file extension of the resource file that is being parsed. Some importers may require this to identify the exact file format.</param>
	/// <returns>True if the requested mesh's geometry could be imported from file stream, false otherwise.</returns>
	bool ImportSurfaceData(in ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKey, out MeshSurfaceData? _outSurfaceData, string? _fileExtension = null);

	#endregion
}
