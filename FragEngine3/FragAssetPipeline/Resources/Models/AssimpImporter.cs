using Assimp;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using System.Numerics;

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
		".blend",	// no longer maintained.
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

	public bool CanImportSubMeshes => true;
	public bool CanImportAnimations => false;
	public bool CanImportMaterials => false;
	public bool CanImportTextures => false;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFileFormatExtensions;

	public bool ImportSurfaceData(in ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKey, out MeshSurfaceData? _outSurfaceData, string? _fileExtension = null)
	{
		if (!TryImportScene(in _importCtx, _resourceFileStream, out Scene? scene, _fileExtension))
		{
			_outSurfaceData = null;
			return false;
		}

		Assimp.Mesh? mesh;
		if (scene!.MeshCount > 1)
		{
			mesh = scene!.Meshes.FirstOrDefault(o => o.PrimitiveType.HasFlag(PrimitiveType.Triangle) && _resourceKey.EndsWith(o.Name, StringComparison.InvariantCultureIgnoreCase));
		}
		else
		{
			mesh = scene!.Meshes.FirstOrDefault(o => o.PrimitiveType.HasFlag(PrimitiveType.Triangle));
		}

		// Extract surface geometry data from mesh:
		if (mesh is null)
		{
			_importCtx.Logger.LogError($"Scene does not contain any meshes with triangular polygon primitive topology and mesh name '{_resourceKey}'!");
			_outSurfaceData = null;
			return false;
		}
		if (mesh.PrimitiveType != PrimitiveType.Triangle)
		{
			_importCtx.Logger.LogWarning("Mesh contains non-triangular polygon primitive topology, which is not fully supported!");
		}

		bool hasPositions = mesh.HasVertices;
		bool hasNormals = mesh.HasNormals;
		bool hasUVs = mesh.TextureCoordinateChannelCount != 0;
		bool hasFullBasicData = hasPositions && hasNormals && hasUVs;

		bool hasTangents = mesh.Tangents is not null && mesh.Tangents.Count != 0;
		bool hasSecondaryUVs = mesh.TextureCoordinateChannelCount > 1;

		if (!mesh.HasFaces)
		{
			_importCtx.Logger.LogError("Mesh imported from 3D model file does not contain any faces!");
			_outSurfaceData = null;
			return false;
		}
		if (!hasPositions)
		{
			_importCtx.Logger.LogError("Mesh imported from 3D model file does not contain any vertex positions!");
			_outSurfaceData = null;
			return false;
		}
		if (!hasFullBasicData)
		{
			_importCtx.Logger.LogWarning("Mesh does not have the full set of basic vertex data; Some shading may not work as intended!");
		}

		// Assemble basic vertex data:
		BasicVertex[] verticesBasic = new BasicVertex[mesh.VertexCount];
		{
			List<Vector3>? uvs0 = hasUVs ? mesh.TextureCoordinateChannels[0] : null;
			for (int i = 0; i < mesh.VertexCount; ++i)
			{
				Vector3 normal = hasNormals ? mesh.Normals![i] : Vector3.UnitY;
				Vector3 uv = hasUVs ? uvs0![i] : Vector3.Zero;

				verticesBasic[i] = new BasicVertex(
					mesh.Vertices![i],
					normal,
					new(uv.X, uv.Y));
			}
		}

		// Optionally, assemble extended vertex data:
		ExtendedVertex[]? verticesExt = null;
		if (hasTangents || hasSecondaryUVs)
		{
			List<Vector3>? uvs1 = hasSecondaryUVs ? mesh.TextureCoordinateChannels[1] : null;
			verticesExt = new ExtendedVertex[mesh.VertexCount];

			for (int i = 0; i < mesh.VertexCount; ++i)
			{
				Vector3 tangents = hasTangents ? mesh.Tangents![i] : Vector3.UnitZ;
				Vector3 uv2 = hasSecondaryUVs ? uvs1![i] : Vector3.Zero;

				verticesExt[i] = new ExtendedVertex(
					tangents,
					new(uv2.X, uv2.Y));
			}
		}

		// Extract indices:
		ushort[]? indices16 = null;
		int[]? indices32 = null;
		int indexCount = mesh.FaceCount * 3;
		var srcIndices = mesh.GetIndices().ToArray();
		if (indexCount > ushort.MaxValue)
		{
			indices32 = new int[indexCount];
			for (int i = 0; i < indexCount; ++i)
			{
				indices32[i] = srcIndices[i];
			}
		}
		else
		{
			indices16 = new ushort[indexCount];
			for (int i = 0; i < indexCount; ++i)
			{
				indices16[i] = (ushort)srcIndices[i];
			}
		}

		// Assemble mesh surface data, and verify validity:
		_outSurfaceData = new()
		{
			verticesBasic = verticesBasic,
			verticesExt = verticesExt,
			indices16 = indices16,
			indices32 = indices32,
		};

		bool isValid = _outSurfaceData.IsValid;
		return isValid;
	}

	public IEnumerator<string> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKeyBase, string? _fileExtension = null)
	{
		if (!TryImportScene(in _importCtx, _resourceFileStream, out Scene? scene, _fileExtension))
		{		
			yield break;
		}

		if (scene!.MeshCount <= 1)
		{
			yield return _resourceKeyBase;
			yield break;
		}

		foreach (Assimp.Mesh mesh in scene!.Meshes)
		{
			if (mesh.HasVertices && mesh.HasFaces)
			{
				string resourceKey = $"{_resourceKeyBase}_{mesh.Name}";
				yield return resourceKey;
			}
		}
	}

	private static bool TryImportScene(in ImporterContext _importCtx, Stream _resourceFileStream, out Scene? _outScene, string? _fileExtension)
	{
		using AssimpContext assimpCtx = new();

		try
		{
			_outScene = assimpCtx.ImportFileFromStream(_resourceFileStream, PostProcessSteps.Triangulate | PostProcessSteps.SortByPrimitiveType, _fileExtension);
		}
		catch (Exception ex)
		{
			_importCtx.Logger.LogException("Assimp failed to import scene from 3D model file stream!", ex);
			_outScene = null;
			return false;
		}

		if (!_outScene.HasMeshes)
		{
			_importCtx.Logger.LogError("Scene imported from 3D model file does not contain any meshes!");
			_outScene = null;
			return false;
		}

		return true;
	}

	#endregion
}
