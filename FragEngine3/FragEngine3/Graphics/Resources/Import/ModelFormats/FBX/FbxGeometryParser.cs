using FragEngine3.EngineCore;
using System.Numerics;
using Vortice.DXGI;

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
			yield return new Vector3(
				(float)vertices[positionIdx + 0],
				(float)vertices[positionIdx + 1],
				(float)vertices[positionIdx + 2]);
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
			Vector2 uv = new((float)uvCoords[0], (float)uvCoords[1]);
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
				yield return new Vector2(
					(float)uvCoords[2 * i + 0],
					(float)uvCoords[2 * i + 1]);
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

				yield return new Vector2(
					(float)uvCoords[uvCoordIdx + 0],
					(float)uvCoords[uvCoordIdx + 1]);
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
			NormalSpace normalSpace = new()
			{
				normal = new((float)normalCoords[0], (float)normalCoords[1], (float)normalCoords[2]),
				binormal = new((float)normalCoords[3], (float)normalCoords[4], (float)normalCoords[5]),
				tangent = new((float)normalCoords[6], (float)normalCoords[7], (float)normalCoords[8]),
			};
			
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
				Vector3 normal = new(
					(float)normalCoords[9 * i + 0],
					(float)normalCoords[9 * i + 1],
					(float)normalCoords[9 * i + 2]);
				Vector3 binormal = new(
					(float)normalCoords[9 * i + 3],
					(float)normalCoords[9 * i + 4],
					(float)normalCoords[9 * i + 5]);
				Vector3 tangent = new(
					(float)normalCoords[9 * i + 6],
					(float)normalCoords[9 * i + 7],
					(float)normalCoords[9 * i + 8]);

				yield return new NormalSpace()
				{
					normal = normal,
					binormal = binormal,
					tangent = tangent,
				};
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

				Vector3 normal = new(
					(float)normalCoords[uvCoordIdx + 0],
					(float)normalCoords[uvCoordIdx + 1],
					(float)normalCoords[uvCoordIdx + 2]);
				Vector3 binormal = new(
					(float)normalCoords[uvCoordIdx + 3],
					(float)normalCoords[uvCoordIdx + 4],
					(float)normalCoords[uvCoordIdx + 5]);
				Vector3 tangent = new(
					(float)normalCoords[uvCoordIdx + 6],
					(float)normalCoords[uvCoordIdx + 7],
					(float)normalCoords[uvCoordIdx + 8]);

				yield return new NormalSpace()
				{
					normal = normal,
					binormal = binormal,
					tangent = tangent,
				};
			}
		}
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
