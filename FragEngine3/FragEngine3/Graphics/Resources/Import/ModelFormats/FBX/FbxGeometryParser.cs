using FragEngine3.EngineCore;
using System.Numerics;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

internal static class FbxGeometryParser
{
	#region Methods

	public static int TryGetVertexCount(FbxNode _geometryNode)
	{
		return FindAndUnpackArrayProperty(_geometryNode, FbxConstants.NODE_NAME_VERTICES, out double[] vertices)
			? vertices.Length / 3
			: 0;
	}

	public static IEnumerator<Vector3> EnumerateVertexPositions(FbxNode _geometryNode)
	{
		if (_geometryNode is null)
		{
			yield break;
		}

		if (!FindAndUnpackArrayProperty(_geometryNode, FbxConstants.NODE_NAME_VERTICES, out double[] vertices))
		{
			Logger.Instance?.LogError("Could not find vertex positions in FBX document!");
			yield break;
		}

		int positionCount = vertices.Length / 3;

		for (int i = 0; i < positionCount; i++)
		{
			int positionIdx = 3 * i;
			yield return ReadVector3FromArray(vertices, positionIdx);
		}
	}

	public static IEnumerator<Vector2> EnumerateVertexUVs(FbxNode _geometryNode, int _expectedVertexCount)
	{
		if (_geometryNode is null)
		{
			yield break;
		}

		if (!_geometryNode.FindChildNode(FbxConstants.NODE_NAME_LAYER_ELEM_UVs, out FbxNode? layerNode))
		{
			Logger.Instance?.LogError("Could not find UV layer elements in FBX document!");
			yield break;
		}
		if (!FindMappingInfo(layerNode!, out FbxMappingType mappingType, out FbxReferenceInfoType refInfoType) ||
			!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_UVs, out double[] uvCoords))
		{
			Logger.Instance?.LogError("Could not find UV coordinates or indices in FBX document!");
			yield break;
		}

		int uvCount = refInfoType == FbxReferenceInfoType.Direct
			? uvCoords.Length / 2
			: _expectedVertexCount;

		// Same value across all vertices:
		if (mappingType == FbxMappingType.AllSame)
		{
			Vector2 uv = ReadVector2FromArray(uvCoords, 0);
			for (int i = 0; i < uvCount; ++i)
			{
				yield return uv;
			}
		}
		// Value array:
		else if (refInfoType == FbxReferenceInfoType.Direct)
		{
			for (int i = 0; i < uvCount; ++i)
			{
				yield return ReadVector2FromArray(uvCoords, 2 * i);
			}
		}
		// Indexed values:
		else
		{
			if (!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_UV_INDEX, out int[] uvIndices))
			{
				Logger.Instance?.LogError("Could not find UV indices in FBX document!");
				yield break;
			}

			for (int i = 0; i < uvCount; i++)
			{
				int uvCoordIdx = 2 * uvIndices[i];
				yield return ReadVector2FromArray(uvCoords, uvCoordIdx);

			}
		}
	}

	public static IEnumerator<NormalSpace> EnumerateVertexNormals(FbxNode _geometryNode, int _expectedVertexCount)
	{
		if (_geometryNode is null)
		{
			yield break;
		}

		if (!_geometryNode.FindChildNode(FbxConstants.NODE_NAME_LAYER_ELEM_NORMAL, out FbxNode? layerNode))
		{
			Logger.Instance?.LogError("Could not find normal layer elements in FBX document!");
			yield break;
		}
		if (!FindMappingInfo(layerNode!, out FbxMappingType mappingType, out FbxReferenceInfoType refInfoType) ||
			!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_NORMALS, out double[] normalCoords))
		{
			Logger.Instance?.LogError("Could not find normal coordinates or indices in FBX document!");
			yield break;
		}

		int normalCount = refInfoType == FbxReferenceInfoType.Direct
			? normalCoords.Length / 9
			: _expectedVertexCount;

		// Same value across all vertices:
		if (mappingType == FbxMappingType.AllSame)
		{
			NormalSpace normalSpace = ReadNormalSpaceFromArray(normalCoords, 0);
			
			for (int i = 0; i < normalCount; ++i)
			{
				yield return normalSpace;
			}
		}
		// Value array:
		else if (refInfoType == FbxReferenceInfoType.Direct)
		{
			for (int i = 0; i < normalCount; ++i)
			{
				yield return ReadNormalSpaceFromArray(normalCoords, 9 * i);
			}
		}
		// Indexed values:
		else
		{
			if (!FindAndUnpackArrayProperty(layerNode!, FbxConstants.NODE_NAME_LAYER_UV_INDEX, out int[] uvIndices))
			{
				Logger.Instance?.LogError("Could not find UV indices in FBX document!");
				yield break;
			}

			for (int i = 0; i < normalCount; i++)
			{
				int uvCoordIdx = 9 * uvIndices[i];
				yield return ReadNormalSpaceFromArray(normalCoords, uvCoordIdx);
			}
		}
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
	private static NormalSpace ReadNormalSpaceFromArray(double[] _coordArray, int _normSpaceStartIdx)
	{
		return new()
		{
			normal = new(
				(float)_coordArray[_normSpaceStartIdx + 0],
				(float)_coordArray[_normSpaceStartIdx + 1],
				(float)_coordArray[_normSpaceStartIdx + 2]),
			binormal = new(
				(float)_coordArray[_normSpaceStartIdx + 3],
				(float)_coordArray[_normSpaceStartIdx + 4],
				(float)_coordArray[_normSpaceStartIdx + 5]),
			tangent = new(
				(float)_coordArray[_normSpaceStartIdx + 6],
				(float)_coordArray[_normSpaceStartIdx + 7],
				(float)_coordArray[_normSpaceStartIdx + 8]),
		};
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
