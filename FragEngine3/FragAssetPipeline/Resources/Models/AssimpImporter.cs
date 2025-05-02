using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Resources;

namespace FragAssetPipeline.Resources.Models;

/// <summary>
/// Importer for various 3D model formats, using the ASSIMP library.
/// </summary>
public sealed class AssimpImporter : IModelImporter
{
	#region Fields

	private static readonly string[] supportedFileFormatExtensions =
	[
		".3ds",
		".blend",
		".dae",
		".fbx",
		".gltf",
		".ma",
		".max",
		".mdl",
		".ms3d",
		".obj",
		".ply",
		".stl",
		".usd",
		".x",
		//...
	];

	#endregion
	#region Properties

	public MeshVertexDataFlags SupportedVertexData => MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	public bool CanImportAnimations => false;
	public bool CanImportMaterials => false;
	public bool CanImportTextures => false;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFileFormatExtensions;

	public bool ImportSurfaceData(in ImporterContext _importCtx, Stream _resourceFileStream, out MeshSurfaceData? _outSurfaceData)
	{
		//TODO

		throw new NotImplementedException(); //TEMP
	}

	public IEnumerator<ResourceHandle> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream)
	{
		yield break;
	}

	#endregion
}
