using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;
using FragEngine3.Resources;
using System.Numerics;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats;

/// <summary>
/// Importer for the FBX 3D file format.
/// </summary>
public class FbxImporter : IModelImporter
{
	#region Fields

	private static readonly string[] supportedFormatExtensions = [ ".fbx" ];

	#endregion
	#region Properties

	public MeshVertexDataFlags SupportedVertexData => MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	public bool CanImportAnimations => false;
	public bool CanImportMaterials => false;
	public bool CanImportTextures => false;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFormatExtensions;

	public bool ImportSurfaceData(in ImporterContext _importCtx, Stream _resourceFileStream, out MeshSurfaceData? _outSurfaceData)  // INCOMPLETE!	
	{
		if (_resourceFileStream is null || !_resourceFileStream.CanRead)
		{
			_importCtx.Logger.LogError("Cannot import FBX document from null or write-only stream!");
			_outSurfaceData = null;
			return false;
		}

		using BinaryReader reader = new(_resourceFileStream);

		// Try reading the FBX file's full data structure as-is:
		if (!FbxDocument.ReadFbxDocument(reader, out FbxDocument? document) || document is null)
		{
			_importCtx.Logger.LogError("Failed to import FBX document, aborting model import!");
			_outSurfaceData = null;
			return false;
		}

		// First, parse only static mesh surface geometry (basic & extended vertex data) from the document:
		if (!ParseSurfaceGeometry(in _importCtx, document, out _outSurfaceData))
		{
			_importCtx.Logger.LogError("Failed to parse surface geometry from FBX document!");
			return false;
		}

		//TODO [later]: Parse animations.

		return true;
	}

	private static bool ParseSurfaceGeometry(in ImporterContext _importCtx, FbxDocument _document, out MeshSurfaceData? _outMeshData)
	{
		// Navigate through node hierarchy, skipping straight to polygonal geometry nodes:
		if (!_document.FindNode(FbxConstants.NODE_NAME_OBJECTS, out FbxNode? objectsNode) || objectsNode is null)
		{
			_importCtx.Logger.LogError("Could not find objects node in FBX document!");
			_outMeshData = null;
			return false;
		}
		if (!objectsNode.FindChildNode(FbxConstants.NODE_NAME_GEOMETRY, out FbxNode? geometryNode) || geometryNode is null)
		{
			_importCtx.Logger.LogError("Could not find geometry node in FBX document!");
			_outMeshData = null;
			return false;
		}

		// Triangle indices:
		if (!FbxGeometryParser.GetTriangleIndices(geometryNode, out int[] vertexIndices, out List<int> indices32, out ushort[]? indices16))
		{
			_outMeshData = null;
			return false;
		}

		// Vertex data:
		List<Vector3> positions = FbxGeometryParser.GetVertexPositions(geometryNode);
		List<Vector3> normals = FbxGeometryParser.GetVertexNormals(geometryNode, indices32);
		List<Vector2> uvs = FbxGeometryParser.GetVertexUVs(geometryNode, indices32);

		// Assemble basic vertex data:
		int vertexCount = vertexIndices.Length;     //TEMP (good enough for now)

		BasicVertex[] vertsBasic = new BasicVertex[vertexCount];

		int[] remappedIndices = new int[indices32.Count];				//TODO: Triangle indices refer to position indices, even though they should be remapped to vertex indices.
		for (int i = 0; i < vertexCount; ++i)
		{
			int positionIdx = vertexIndices[i];

			vertsBasic[i] = new(positions[positionIdx], normals[i], uvs[i]);
		}


		//TODO 1 [later]: Parse extended vertex data.
		//TODO 2 [later]: Parse blend shapes
		//TODO 3 [later]: Parse rigging


		// Assemble mesh data and return success:
		_outMeshData = new()
		{
			verticesBasic = vertsBasic,
			verticesExt = null,
			indices16 = indices16,
			indices32 = indices16 is not null ? indices32.ToArray() : null,
		};
		return true;
	}

	public IEnumerator<ResourceHandle> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream)
	{
		yield break;
	}

	#endregion
}
