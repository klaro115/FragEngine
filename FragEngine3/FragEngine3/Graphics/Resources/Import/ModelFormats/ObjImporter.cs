using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats;

public static class ObjImporter
{
	#region Types

	private ref struct Buffers
	{
		public Buffers()
		{
			bytes = new byte[1024];
			utf16 = new char[1024];
		}

		public readonly byte[] bytes;
		public readonly char[] utf16;

		public int byteLength = 0;
		public int charLength = 0;
		public bool eof = false;
	}

	private struct VertexIndices : IEquatable<VertexIndices>
	{
		public int position;
		public int normal;
		public int uv;

		public static readonly VertexIndices none = new()
		{
			position = -1,
			normal = -1,
			uv = -1,
		};

		public readonly bool Equals(VertexIndices other)
		{
			return position == other.position && normal == other.normal && uv == other.uv;
		}
		public override readonly bool Equals(object? obj)
		{
			return obj is VertexIndices other && Equals(other);
		}

		public static bool operator ==(VertexIndices a, VertexIndices b) => a.Equals(b);
		public static bool operator !=(VertexIndices a, VertexIndices b) => !a.Equals(b);

		public override readonly int GetHashCode() => position << 16 | normal ^ uv;
	}

	private sealed class SubMesh
	{
		public int startIdx;
		public string name = string.Empty;
	}

	private sealed class GeometryData
	{
		public readonly List<Vector3> positions = new(256);
		public readonly List<Vector3> normals = new(256);
		public readonly List<Vector2> uvs = new(256);
		public readonly List<VertexIndices> vertexIndices = new(256);
		public readonly List<SubMesh> subMeshes = new(4);
		public readonly List<string> subMeshMaterialNames = new(4);

		public int MaxVertexElementCount => Math.Max(Math.Max(positions.Count, normals.Count), uvs.Count);
	}

	#endregion
	#region Fields

	/// <summary>
	/// Invariant culture is used and assumed when parsing floating-point values from string. The decimal separator must be a period '.'; commas are not supported!
	/// </summary>
	private static readonly CultureInfo culture = CultureInfo.InvariantCulture;

	#endregion
	#region Constants

	private const string KEYWORD_MATERIAL_LIBRARY = "mtllib ";
	private const string KEYWORD_SUBMESH_MATERIAL = "usemtl ";

	#endregion
	#region Methods

	public static bool ImportModel(Stream _streamUtf8, out MeshSurfaceData? _outMeshData) //TODO: Output a list of submeshes as individual meshes instead, or implement composite mesh type.
	{
		if (_streamUtf8 == null)
		{
			Logger.Instance?.LogError("Cannot import OBJ model from null stream!");
			_outMeshData = null;
			return false;
		}
		if (!_streamUtf8.CanRead)
		{
			Logger.Instance?.LogError("Cannot import OBJ model from write-only stream!");
			_outMeshData = null;
			return false;
		}

		GeometryData geometryData = new();

		// Read lines one after another:
		Buffers buffers = new();
		do
		{
			if (ReadLine(_streamUtf8, ref buffers))
			{
				ParseLine(in buffers, geometryData);
			}
		}
		while (!buffers.eof);

		// Remove duplicate vertex index permutations and generate indices:
		//int posCount = geometryData.positions.Count;
		int normCount = geometryData.normals.Count;
		int uvCount = geometryData.uvs.Count;
		int indexCount = geometryData.vertexIndices.Count;

		int[] indexRemapped = new int[indexCount];
		Array.Fill(indexRemapped, -1);
		List<BasicVertex> uniqueVertices = new(geometryData.MaxVertexElementCount);

		for (int i = 0; i < indexCount; ++i)
		{
			// If non-duplicate:
			if (indexRemapped[i] != -1) continue;

			// Assemble new vertex data:
			VertexIndices newIndices = geometryData.vertexIndices[i];
			Vector3 position = geometryData.positions[newIndices.position];
			Vector3 normal = newIndices.normal >= 0 && newIndices.normal < normCount ? geometryData.normals[newIndices.normal] : Vector3.UnitY;
			Vector2 uv = newIndices.uv >= 0 && newIndices.uv < uvCount ? geometryData.uvs[newIndices.uv] : Vector2.Zero;

			// Mark vertex as unique by updating its index:
			int newVertexIndex = uniqueVertices.Count;
			indexRemapped[i] = newVertexIndex;
			uniqueVertices.Add(new BasicVertex(position, normal, uv));

			// Find all other triangle indices that map to this same vertex definition:
			for (int j = i + 1; j < indexCount; ++j)
			{
				if (indexRemapped[j] != -1) continue;

				VertexIndices otherIndices = geometryData.vertexIndices[j];
				if (newIndices.Equals(otherIndices))
				{
					indexRemapped[j] = newVertexIndex;
				}
			}
		}

		// Convert indices to an appropriately sized integer format: (16bit ot 32bit)
		ushort[]? triangleIndices16 = null;
		int[]? triangleIndices32 = null;
		if (indexRemapped.Length < ushort.MaxValue)
		{
			triangleIndices16 = new ushort[indexRemapped.Length];
			for (int i = 0; i < indexRemapped.Length; ++i)
			{
				triangleIndices16[i] = (ushort)indexRemapped[i];
			}
		}
		else
		{
			triangleIndices32 = indexRemapped;
		}

		// Assemble mesh data and return success if valid:
		_outMeshData = new()
		{
			//verticesBasic = uniqueVertices[0..uniqueVertexCount],
			verticesBasic = [.. uniqueVertices],
			verticesExt = null,

			indices16 = triangleIndices16,
			indices32 = triangleIndices32,
		};
		return _outMeshData.IsValid;
	}

	private static bool ReadLine(Stream _streamUtf8, ref Buffers _buffers)
	{
		const byte STREAM_EOF = 0xFF;   // ReadByte returns -1 on EoF.
		const byte LINE_FEED = 0x0A;
		const byte CARRIAGE_RETURN = 0x0D;
		const byte UTF8_INVALID = 0xF5;

		// Reset buffers for the new line:
		_buffers.byteLength = 0;
		_buffers.charLength = 0;
		_buffers.eof = false;

		// Read raw UTF-8 bytes:
		byte c;
		while (
			!(_buffers.eof = (c = (byte)_streamUtf8.ReadByte()) == STREAM_EOF) &&
			c != LINE_FEED &&
			c != CARRIAGE_RETURN &&
			c < UTF8_INVALID)
		{
			_buffers.bytes[_buffers.byteLength++] = c;
		}

		// Skip lines that are too short to contain valid information:
		if (_buffers.byteLength < 3) return false;

		// Add null-terminator:
		_buffers.bytes[_buffers.byteLength++] = 0;

		// Convert UTF-8 bytes to UTF-16 string:
		_buffers.charLength = Encoding.UTF8.GetChars(_buffers.bytes, 0, _buffers.byteLength, _buffers.utf16, 0);

		return _buffers.charLength != 0;
	}

	private static bool ParseLine(in Buffers _buffers, GeometryData _geometryData)
	{
		string txt;
		char prefix = _buffers.utf16[0];
		char type;

		switch (prefix)
		{
			// Parse a single piece of vertex data:
			case 'v':
				type = _buffers.utf16[1];
				return type switch
				{
					'n' => ParseVector3(_buffers, _geometryData.normals),   // Normal vector
					't' => ParseVector2(_buffers, _geometryData.uvs),       // Texture coordinate
					_ => ParseVector3(_buffers, _geometryData.positions),   // Position
				};

			// Parse a single face's indices:
			case 'f':
				return ParseFace(_buffers, _geometryData.vertexIndices);

			// Start a new submesh:
			case 'o':

				txt = new string(_buffers.utf16, 2, _buffers.charLength - 3);
				_geometryData.subMeshes.Add(new SubMesh()
				{
					name = txt,
					startIdx = _geometryData.vertexIndices.Count,
				});
				return true;

			// Material library (list of names):
			case 'm':
				txt = new string(_buffers.utf16, 0, _buffers.charLength);
				if (txt.StartsWith(KEYWORD_MATERIAL_LIBRARY, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;    // skip, as we'll just use per-submesh declarations.
				}
				return false;

			// Submesh material name:
			case 'u':
				txt = new string(_buffers.utf16, 0, _buffers.charLength);
				if (txt.StartsWith(KEYWORD_SUBMESH_MATERIAL, StringComparison.InvariantCultureIgnoreCase))
				{
					return ParseSubMeshMaterial(txt, _geometryData.subMeshes, _geometryData.subMeshMaterialNames);
				}
				return false;

			// skip smoothing groups:
			case 's':
				return true;

			// Skip comments:
			case '#':
				return true;

			default:
				return false;
		}
	}

	private static bool ParseVector3(in Buffers _buffers, List<Vector3> _vectors)
	{
		Vector3 v = Vector3.Zero;
		int componentIdx = 0;

		for (int i = 2; i < _buffers.charLength; ++i)
		{
			// Find next part:
			if (_buffers.utf16[i] != ' ')
			{
				int endIdx = i + 1;
				for (; endIdx < _buffers.charLength; endIdx++)
				{
					char c = _buffers.utf16[endIdx];
					if (c == ' ' || c == '\n')
					{
						break;
					}
				}

				// Set value on vector:
				ReadOnlySpan<char> span = _buffers.utf16.AsSpan(i, endIdx - i);
				if (float.TryParse(span, culture, out float value))
				{
					v[componentIdx] = value;
				}
				componentIdx++;
				if (componentIdx == 3) break;

				// Skip to end of current part:
				i = endIdx;
			}
		}
		_vectors.Add(v);
		return true;
	}

	private static bool ParseVector2(in Buffers _buffers, List<Vector2> _vectors)
	{
		Vector2 v = Vector2.Zero;
		int componentIdx = 0;

		for (int i = 2; i < _buffers.charLength; ++i)
		{
			// Find next part:
			if (_buffers.utf16[i] != ' ')
			{
				int endIdx = i + 1;
				for (; endIdx < _buffers.charLength; endIdx++)
				{
					char c = _buffers.utf16[endIdx];
					if (c == ' ' || c == '\n')
					{
						break;
					}
				}

				// Set value on vector:
				ReadOnlySpan<char> span = _buffers.utf16.AsSpan(i, endIdx - i);
				if (float.TryParse(span, culture, out float value))
				{
					v[componentIdx] = value;
				}
				componentIdx++;
				if (componentIdx == 2) break;

				// Skip to end of current part:
				i = endIdx;
			}
		}
		_vectors.Add(v);
		return true;
	}

	private static bool ParseFace(in Buffers _buffers, List<VertexIndices> _indices)
	{
		if (_buffers.charLength < 7) return false;

		VertexIndices vi = VertexIndices.none;
		VertexIndices[] curFaceIndices = new VertexIndices[4];
		int vertexIdx = 0;  // 0..3
		int indexType = 0;  // 0=P, 1=T, 2=N

		// Iterate over all characters in this line:
		for (int i = 2; i < _buffers.charLength; ++i)
		{
			char c = _buffers.utf16[i];
			if (char.IsAsciiDigit(c))
			{
				int endIdx = i + 1;
				for (; endIdx < _buffers.charLength; endIdx++)
				{
					c = _buffers.utf16[endIdx];
					if (!char.IsAsciiDigit(c))
					{
						break;
					}
				}
				int spanLength = endIdx - i;

				// Set value on face indices:
				ReadOnlySpan<char> span = _buffers.utf16.AsSpan(i, spanLength);
				if (int.TryParse(span, out int value))
				{
					value -= 1; // OBJ uses 1-based indices, ours are 0-based.
					if (indexType == 0) vi.position = value;
					else if (indexType == 1) vi.uv = value;
					else if (indexType == 2) vi.normal = value;
				}
				if (spanLength > 1)
				{
					i += spanLength - 1;
				}
			}
			// Advance to next component:
			else if (c == '/')
			{
				if (i + 1 < _buffers.charLength && _buffers.utf16[i + 1] == '/')
				{
					// Format must be: 'f 0//1 2//3 4//5' (PN)
					indexType = 2;
					i++;
				}
				else
				{
					// Format is either: 'f 0/1 2/3 4/5' (PT) or 'f 0/1/2 3/4/5 6/7/8' (PTN)
					indexType++;
				}
			}
			// Advance to next vertex:
			else if (c == ' ' || c == '\t')
			{
				curFaceIndices[vertexIdx++] = vi;
				if (vertexIdx == 4) break;

				vi = VertexIndices.none;
				indexType = 0;
			}
			// End of line or file, end face:
			else if (c == '\n' || c == '\r' || c == '\0')
			{
				curFaceIndices[vertexIdx++] = vi;
				break;
			}
		}

		// Discard any faces containing less than 3 vertices:
		if (vertexIdx < 3) return false;

		// Push data to the mesh's vertex index list in triangle index order:
		_indices.Add(curFaceIndices[0]);
		_indices.Add(curFaceIndices[1]);
		_indices.Add(curFaceIndices[2]);
		if (vertexIdx == 4)
		{
			_indices.Add(curFaceIndices[0]);
			_indices.Add(curFaceIndices[2]);
			_indices.Add(curFaceIndices[3]);
		}
		return true;
	}

	private static bool ParseSubMeshMaterial(string _line, List<SubMesh> _subMeshes, List<string> _subMeshMaterialNames)
	{
		string mtlName = _line[KEYWORD_SUBMESH_MATERIAL.Length..];

		// If a material name was already given for this submesh, overwrite it with this new declaration:
		if (_subMeshMaterialNames.Count != 0 && _subMeshMaterialNames.Count >= _subMeshes.Count)
		{
			_subMeshMaterialNames[^1] = mtlName;
			return true;
		}

		// Assign last used material to all preceding submeshes that did not have another material listed:
		if (_subMeshMaterialNames.Count < _subMeshes.Count - 1)
		{
			string lastMtlName = _subMeshMaterialNames.LastOrDefault() ?? mtlName;
			for (int i = _subMeshMaterialNames.Count; i < _subMeshes.Count - 1; ++i)
			{
				_subMeshMaterialNames.Add(lastMtlName);
			}
		}

		// Assign new material name to current submesh:
		_subMeshMaterialNames.Add(mtlName);

		return true;
	}

	#endregion
}
