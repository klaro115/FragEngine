using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;
using System.Numerics;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats;

public static class FbxImporter
{
	#region Methods

	public static bool ImportModel(Stream _streamBytes, out MeshSurfaceData? _outMeshData)
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

		//TODO

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

		// Vertex positions:
		if (!FindAndUnpackArrayProperty(geometryNode, FbxConstants.NODE_NAME_VERTICES, out double[] vertices))
		{
			Logger.Instance?.LogError("Could not find vertex positions in FBX document!");
			_outMeshData = null;
			return false;
		}
		int positionCount = vertices.Length / 3;

		// Triangle indices:
		if (!FindAndUnpackArrayProperty(geometryNode, FbxConstants.NODE_NAME_INDICES, out int[] indices32))
		{
			Logger.Instance?.LogError("Could not find triangle indices in FBX document!");
			_outMeshData = null;
			return false;
		}
		ushort[]? indices16 = null;
		if (indices32.Length <= ushort.MaxValue)
		{
			indices16 = new ushort[indices32.Length];
			for (int i = 0; i < indices32.Length; i++)
			{
				indices16[i] = (ushort)indices32[i];
			}
		}


		//TODO 1: Parse normals & UVs
		//TODO 2: Parse extended data
		//TODO 3 [later]: Parse blend shapes
		//TODO 3 [later]: Parse bone animations


		// Assemble basic vertex data:
		BasicVertex[] vertsBasic = new BasicVertex[positionCount];
		for (int i = 0; i < positionCount; ++i)
		{
			Vector3 position = new(
				(float)vertices[3 * i + 0],
				(float)vertices[3 * i + 1],
				(float)vertices[3 * i + 2]);
			Vector3 normal = Vector3.UnitY;
			Vector2 uv = Vector2.Zero;

			vertsBasic[i] = new(position, normal, uv);
		}

		//TODO: Assemble extended vertex data.

		// Assemble mesh data and return success:
		_outMeshData = new()
		{
			verticesBasic = vertsBasic,
			verticesExt = null,						//TEMP, TODO
			indices16 = indices16,
			indices32 = indices32,
		};
		return true;
	}

	private static bool FindAndUnpackArrayProperty<T>(FbxNode _parentNode, string _nodeName, out T[] _outArrayValues) where T : unmanaged
	{
		if (!_parentNode.FindChildNode(_nodeName, out FbxNode? node) || node is null)
		{
			goto abort;
		}
		if (node.PropertyCount == 0)
		{
			goto abort;
		}
		if (!node.GetProperty(0, out FbxProperty property) || property is not FbxPropertyArray<T> arrayProperty)
		{
			goto abort;
		}

		_outArrayValues = arrayProperty.values;
		return true;

	abort:
		_outArrayValues = [];
		return false;
	}

	#endregion
}
