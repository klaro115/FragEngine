using FragEngine3.EngineCore;
using System.Numerics;

namespace FragAssetFormats.Geometry.FBX;

internal static class FbxGeometryParser
{
	#region Methods

	public static int TryGetVertexCount(FbxNode _geometryNode)      //TODO: INCORRECT
	{
		return FindAndUnpackArrayProperty(_geometryNode, FbxConstants.NODE_NAME_VERTICES, out double[] vertices)
			? vertices.Length / 3
			: 0;
	}

	public static List<Vector3> GetVertexPositions(FbxNode _geometryNode)
	{
		if (_geometryNode is null) return [];

		if (!FindAndUnpackArrayProperty(_geometryNode, FbxConstants.NODE_NAME_VERTICES, out double[] vertices))
		{
			Logger.Instance?.LogError("Could not find vertex positions in FBX document!");
			return [];
		}

		int positionCount = vertices.Length / 3;
		List<Vector3> positions = new(positionCount);

		for (int i = 0; i < positionCount; i++)
		{
			Vector3 position = ReadVector3FromArray(vertices, 3 * i);
			positions.Add(position);
		}

		return positions;
	}

	public static List<Vector2> GetVertexUVs(FbxNode _geometryNode, IList<int> _triangleIndices)
	{
		if (_geometryNode is null)
		{
			return [];
		}

		if (!_geometryNode.FindChildNode(FbxConstants.NODE_NAME_LAYER_ELEM_UVs, out FbxNode? layerNode))
		{
			Logger.Instance?.LogError("Could not find UV layer elements in FBX document!");
			return [];
		}
		if (!FindMappingInfo(layerNode!, out FbxMappingType mappingType, out FbxReferenceInfoType refInfoType) ||
			!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_UVs, out double[] uvCoords))
		{
			Logger.Instance?.LogError("Could not find UV coordinates or indices in FBX document!");
			return [];
		}

		int uvCount = refInfoType == FbxReferenceInfoType.Direct
			? uvCoords.Length / 2
			: _triangleIndices.Count;

		List<Vector2> uvs = new(uvCount);

		// Same value across all vertices:
		if (mappingType == FbxMappingType.AllSame)
		{
			Vector2 uv = ReadVector2FromArray(uvCoords, 0);
			for (int i = 0; i < uvCount; ++i)
			{
				uvs.Add(uv);
			}
		}
		// Value array:
		else if (refInfoType == FbxReferenceInfoType.Direct)
		{
			for (int i = 0; i < uvCount; ++i)
			{
				Vector2 uv = ReadVector2FromArray(uvCoords, 2 * i);
				uvs.Add(uv);
			}
		}
		// Indexed values:
		else
		{
			if (!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_UV_INDEX, out int[] uvIndices))
			{
				Logger.Instance?.LogError("Could not find UV indices in FBX document!");
				return [];
			}
			uvCount = uvIndices.Length;

			for (int i = 0; i < uvCount; i++)
			{
				int uvCoordIdx = 2 * uvIndices[i];
				Vector2 uv = ReadVector2FromArray(uvCoords, uvCoordIdx);
				uvs.Add(uv);
			}
		}
		return uvs;
	}

	public static List<Vector3> GetVertexNormals(FbxNode _geometryNode, IList<int> _triangleIndices)
	{
		if (_geometryNode is null)
		{
			return [];
		}

		if (!_geometryNode.FindChildNode(FbxConstants.NODE_NAME_LAYER_ELEM_NORMAL, out FbxNode? layerNode))
		{
			Logger.Instance?.LogError("Could not find normal layer elements in FBX document!");
			return [];
		}
		if (!FindMappingInfo(layerNode!, out FbxMappingType mappingType, out FbxReferenceInfoType refInfoType) ||
			!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_NORMALS, out double[] normalCoords))
		{
			Logger.Instance?.LogError("Could not find normal coordinates or indices in FBX document!");
			return [];
		}

		int normalCount = refInfoType == FbxReferenceInfoType.Direct
			? normalCoords.Length / 3
			: _triangleIndices.Count;

		List<Vector3> normals = new(normalCount);

		// Same value across all vertices:
		if (mappingType == FbxMappingType.AllSame)
		{
			Vector3 normal = ReadVector3FromArray(normalCoords, 0);

			for (int i = 0; i < normalCount; ++i)
			{
				normals.Add(normal);
			}
		}
		// Value array:
		else if (refInfoType == FbxReferenceInfoType.Direct)
		{
			for (int i = 0; i < normalCount; ++i)
			{
				Vector3 normal = ReadVector3FromArray(normalCoords, 3 * i);
				normals.Add(normal);
			}
		}
		// Indexed values:
		else
		{
			if (!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_NORMALS, out int[] uvIndices))
			{
				Logger.Instance?.LogError("Could not find normal indices in FBX document!");
				return [];
			}
			normalCount = uvIndices.Length;

			for (int i = 0; i < normalCount; i++)
			{
				int uvCoordIdx = 3 * uvIndices[i];
				Vector3 normal = ReadVector3FromArray(normalCoords, uvCoordIdx);
				normals.Add(normal);
			}
		}
		return normals;
	}

	public static bool GetTriangleIndices(FbxNode _geometryNode, out int[] _outRawIndices, out List<int> _outIndices32, out ushort[]? _outIndices16)
	{
		_outIndices16 = null;
		if (_geometryNode is null)
		{
			_outRawIndices = [];
			_outIndices32 = [];
			return false;
		}

		if (!FindAndUnpackArrayProperty(_geometryNode, FbxConstants.NODE_NAME_INDICES, out _outRawIndices))
		{
			Logger.Instance?.LogError("Could not find triangle indices in FBX document!");
			_outRawIndices = [];
			_outIndices32 = [];
			return false;
		}

		// Polygons may contain more than 3 vertices:
		_outIndices32 = new(_outRawIndices.Length);
		for (int i = 0; i < _outRawIndices.Length;)
		{
			int indexA = _outRawIndices[i + 0];
			int indexB = _outRawIndices[i + 1];
			int indexC = _outRawIndices[i + 2];
			if (indexC < 0)     // Note: Negative indices indicate end of a polygon.
			{
				indexC = -indexC - 1;
				_outRawIndices[i + 2] = indexC;

				_outIndices32.Add(indexA);
				_outIndices32.Add(indexB);
				_outIndices32.Add(indexC);

				i += 3;
			}
			else
			{
				int indexD = -_outRawIndices[i + 3] - 1;
				_outRawIndices[i + 3] = indexD;

				_outIndices32.Add(indexA);
				_outIndices32.Add(indexD);
				_outIndices32.Add(indexB);

				_outIndices32.Add(indexA);
				_outIndices32.Add(indexC);
				_outIndices32.Add(indexD);

				i += 4;
			}
		}

		if (_outIndices32.Count <= ushort.MaxValue)
		{
			_outIndices16 = new ushort[_outIndices32.Count];
			for (int i = 0; i < _outIndices32.Count; i++)
			{
				_outIndices16[i] = (ushort)_outIndices32[i];
			}
		}
		return true;
	}

	private static Vector2 ReadVector2FromArray(double[] _coordArray, int _vectorStartIdx)
	{
		return new(
			(float)_coordArray[_vectorStartIdx + 0],
			(float)_coordArray[_vectorStartIdx + 1]);
	}
	private static Vector3 ReadVector3FromArray(double[] _coordArray, int _vectorStartIdx)
	{
		return new(
			(float)_coordArray[_vectorStartIdx + 0],
			(float)_coordArray[_vectorStartIdx + 1],
			(float)_coordArray[_vectorStartIdx + 2]);
	}

	private static bool FindMappingInfo(FbxNode _layerNode, out FbxMappingType _outMappingType, out FbxReferenceInfoType _outRefInfoType)
	{
		if (!FindAndUnpackStringProperty(_layerNode, FbxConstants.NODE_NAME_MAPPING_TYPE, out string mappingTxt) ||
			!FindAndUnpackStringProperty(_layerNode, FbxConstants.NODE_NAME_REF_INFO_TYPE, out string refInfoTypeTxt))
		{
			_outRefInfoType = 0;
			_outMappingType = 0;
			return false;
		}

		_outMappingType = mappingTxt switch
		{
			"ByPolygonVertex" => FbxMappingType.ByPolygonVertices,
			"AllSame" => FbxMappingType.AllSame,
			_ => 0,
		};
		_outRefInfoType = refInfoTypeTxt switch
		{
			"Direct" => FbxReferenceInfoType.Direct,
			"IndexToDirect" => FbxReferenceInfoType.IndexToDirect,
			_ => 0,
		};
		return true;
	}

	public static bool FindAndUnpackStringProperty(FbxNode _parentNode, string _nodeName, out string _outTxt)
	{
		if (FindPropertyInChildNode(_parentNode, _nodeName, out FbxProperty property) && property is FbxPropertyString stringProperty)
		{
			_outTxt = stringProperty.text;
			return true;
		}

		_outTxt = string.Empty;
		return false;
	}

	public static bool FindAndUnpackValueProperty<T>(FbxNode _parentNode, string _nodeName, out T _outValue) where T : unmanaged
	{
		if (FindPropertyInChildNode(_parentNode, _nodeName, out FbxProperty property) && property is FbxProperty<T> valueProperty)
		{
			_outValue = valueProperty.value;
			return true;
		}

		_outValue = default;
		return false;
	}

	public static bool FindAndUnpackArrayProperty<T>(FbxNode _parentNode, string _nodeName, out T[] _outArrayValues) where T : unmanaged
	{
		if (FindPropertyInChildNode(_parentNode, _nodeName, out FbxProperty property) && property is FbxPropertyArray<T> arrayProperty)
		{
			_outArrayValues = arrayProperty.values;
			return true;
		}

		_outArrayValues = [];
		return false;
	}

	private static bool FindPropertyInChildNode(FbxNode _parentNode, string _nodeName, out FbxProperty _outProperty)
	{
		if (!_parentNode.FindChildNode(_nodeName, out FbxNode? node) || node is null)
		{
			goto abort;
		}
		if (node.PropertyCount == 0)
		{
			goto abort;
		}

		return node.GetProperty(0, out _outProperty);

	abort:
		_outProperty = null!;
		return false;
	}

	#endregion
}
