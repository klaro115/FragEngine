using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;
using System.Numerics;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats;

public static class FbxImporter
{
	#region Methods

	public static bool ImportModel(Stream _streamBytes, out MeshSurfaceData? _outMeshData)		// INCOMPLETE!
	{
		if (_streamBytes is null || !_streamBytes.CanRead)
		{
			Logger.Instance?.LogError("Cannot import FBX document from null or write-only stream!");
			_outMeshData = null;
			return false;
		}

		using BinaryReader reader = new(_streamBytes);

		// Try reading the FBX file's full data structure as-is:
		if (!FbxDocument.ReadFbxDocument(reader, out FbxDocument? document) || document is null)
		{
			Logger.Instance?.LogError("Failed to import FBX document, aborting model import!");
			_outMeshData = null;
			return false;
		}

		// First, parse only static mesh surface geometry (basic & extended vertex data) from the document:
		if (!ParseSurfaceGeometry(document, out _outMeshData))
		{
			Logger.Instance?.LogError("Failed to parse surface geometry from FBX document!");
			return false;
		}

		//TODO [later]: Parse animations.

		return true;
	}

	private static bool ParseSurfaceGeometry(FbxDocument _document, out MeshSurfaceData? _outMeshData)
	{
		// Navigate through node hierarchy, skipping straight to polygonal geometry nodes:
		if (!_document.FindNode(FbxConstants.NODE_NAME_OBJECTS, out FbxNode? objectsNode) || objectsNode is null)
		{
			Logger.Instance?.LogError("Could not find objects node in FBX document!");
			_outMeshData = null;
			return false;
		}
		if (!objectsNode.FindChildNode(FbxConstants.NODE_NAME_GEOMETRY, out FbxNode? geometryNode) || geometryNode is null)
		{
			Logger.Instance?.LogError("Could not find geometry node in FBX document!");
			_outMeshData = null;
			return false;
		}

		// Triangle indices:
		if (!FbxGeometryParser.GetTriangleIndices(geometryNode, out int[] indices32, out ushort[]? indices16))
		{
			_outMeshData = null;
			return false;
		}

		int minVertexCount = FbxGeometryParser.TryGetVertexCount(geometryNode);

		// Vertex data:
		List<Vector3> positions = FbxGeometryParser.GetVertexPositions(geometryNode);
		List<Vector3> normals = FbxGeometryParser.GetVertexNormals(geometryNode, indices32);
		List<Vector2> uvs = FbxGeometryParser.GetVertexUVs(geometryNode, indices32);

		// Assemble basic vertex data:
		int vertexCount = indices32.Length;     //TEMP

		BasicVertex[] vertsBasic = new BasicVertex[vertexCount];

		for (int i = 0; i < vertexCount; ++i)
		{
			int positionIdx = indices32[i];

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
			indices32 = indices32,
		};
		return true;
	}

	#endregion
}
