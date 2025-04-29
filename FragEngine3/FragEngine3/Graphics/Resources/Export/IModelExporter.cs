using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;

namespace FragEngine3.Graphics.Resources.Export;

/// <summary>
/// Interface of all exporter types that can export and serialize the geometry data of 3D models to some resources file format.
/// </summary>
public interface IModelExporter : IGraphicsResourceImporter
{
	#region Properties

	// GEOMETRY SUPPORT:

	/// <summary>
	/// Gets flags of all supported vertex data that can be included in the export.
	/// </summary>
	MeshVertexDataFlags SupportedVertexData { get; }
	/// <summary>
	/// Gets whether the output formats support 16-bit index data. This should be true for practically all 3D formats.<para/>
	/// Note: Meshes with 16-bit triangle indices can contain up to 65336 vertices.
	/// </summary>
	bool Supports16BitIndices { get; }
	/// <summary>
	/// Gets whether the output formats support 32-bit index data.<para/>
	/// Note: Meshes with 32-bit triangle indices can contain up to 4.29 billion vertices.
	/// </summary>
	bool Supports32BitIndices { get; }

	// ANIMATION SUPPORT:

	/// <summary>
	/// Gets whether this exporter supports writing out morph targets for vertices' blend shapes.
	/// </summary>
	bool CanExportBlendTargets { get; }
	/// <summary>
	/// Gets whether this exporter supports writing out bone animations.<para/>
	/// Note: Individual animations are treated as separate resources by the Fragment engine.
	/// </summary>
	bool CanExportAnimations { get; }
	/// <summary>
	/// Gets whether this exporter supports bundling material assets in a 3D model file.
	/// </summary>
	bool CanExportMaterials { get; }
	/// <summary>
	/// Gets whether this exporter supports bundling texture assets in a 3D model file.
	/// </summary>
	bool CanExportTextures { get; }

	#endregion
	#region Methods

	bool ExportShaderData(in ImporterContext _exportCtx, MeshSurfaceData _surfaceData, Stream _outputResourceStream);

	#endregion
}
