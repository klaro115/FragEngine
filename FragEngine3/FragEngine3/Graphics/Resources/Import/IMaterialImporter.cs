using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Interface for all importer types that can read and process material data from file.
/// </summary>
public interface IMaterialImporter : IGraphicsResourceImporter
{
	#region Properties

	/// <summary>
	/// Gets whether this importer supports materials of type <see cref="DefaultSurfaceMaterial"/>.
	/// </summary>
	public bool CanImportDefaultMaterials { get; }
	/// <summary>
	/// Gets whether this importer supports material types for surface geometry, inheriting from <see cref="SurfaceMaterial"/>.
	/// </summary>
	public bool CanImportSurfaceMaterials { get; }
	/// <summary>
	/// Gets whether this importer supports material types that use compute shaders, and thus do not use a rasterized graphics pipeline.
	/// </summary>
	public bool CanImportComputeMaterials { get; }

	#endregion
	#region Methods

	/// <summary>
	/// Gets a read-only dictionary of all material types that are supported by this importer, mapped onto the type's name.
	/// </summary>
	/// <returns>A dictionary of all supported material class types.</returns>
	public IReadOnlyDictionary<string, Type> GetSupportedMaterialTypes();

	/// <summary>
	/// Tries to read and deserialize material data from a resource file stream.
	/// </summary>
	/// <param name="_importCtx">A context object providing additional instructions and metadata for importing the material's data.</param>
	/// <param name="_resourceFileStream">A stream, typically from a resource file, from which the material's serialized data can be read.</param>
	/// <param name="_outMaterialData">Outputs the deserialized material data from which a material resource can be constructed. Null on failure.</param>
	/// <returns>True if the resource's data could be imported successfully, false otherwise.</returns>
	public bool ImportMaterialData(in ImporterContext _importCtx, Stream _resourceFileStream, out MaterialDataNew? _outMaterialData);

	/// <summary>
	/// Tries to create a new material resource instance from deserialized data.
	/// </summary>
	/// <param name="_resourceHandle">The resource handle that will own and manage the new material resource instance.</param>
	/// <param name="_graphicsCore">The graphics core that will be used for rendering this material.</param>
	/// <param name="_materialData">Deserialized material data from which to construct the material resource.</param>
	/// <param name="_outMaterial">Outputs a new material resource. Null on failure.</param>
	/// <returns>True if the resource could be created successfully, false otherwise.</returns>
	public bool CreateMaterial(in ResourceHandle _resourceHandle, in GraphicsCore _graphicsCore, in MaterialDataNew _materialData, out Material? _outMaterial);

	#endregion
}
