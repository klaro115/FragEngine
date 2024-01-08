using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using System.Numerics;

namespace FragEngine3.Graphics.Resources
{
	public static class MeshPrimitiveFactory
	{
		#region Methods

		/// <summary>
		/// Creates surface geometry data for a simple cube.
		/// </summary>
		/// <param name="_size">The size of the cube along all three axes.</param>
		/// <param name="_useExtendedData">Whether to generate extended vertex data (tangents, secondary UVs) for this mesh.</param>
		public static MeshSurfaceData CreateCubeData(Vector3 _size, bool _useExtendedData)
		{
			float x = _size.X * 0.5f;
			float y = _size.Y * 0.5f;
			float z = _size.Z * 0.5f;

			return new()
			{
				verticesBasic =
				[
					// Top
					new BasicVertex(new(-x,  y, -z),  Vector3.UnitY, new(0, 0)),	//0
					new BasicVertex(new( x,  y, -z),  Vector3.UnitY, new(1, 0)),
					new BasicVertex(new(-x,  y,  z),  Vector3.UnitY, new(0, 1)),
					new BasicVertex(new( x,  y,  z),  Vector3.UnitY, new(1, 1)),

					// Bottom
					new BasicVertex(new(-x, -y, -z), -Vector3.UnitY, new(0, 0)),	//4
					new BasicVertex(new( x, -y, -z), -Vector3.UnitY, new(1, 0)),
					new BasicVertex(new(-x, -y,  z), -Vector3.UnitY, new(0, 1)),
					new BasicVertex(new( x, -y,  z), -Vector3.UnitY, new(1, 1)),

					// Back
					new BasicVertex(new(-x, -y, -z), -Vector3.UnitZ, new(0, 0)),	//8
					new BasicVertex(new( x, -y, -z), -Vector3.UnitZ, new(1, 0)),
					new BasicVertex(new(-x,  y, -z), -Vector3.UnitZ, new(0, 1)),
					new BasicVertex(new( x,  y, -z), -Vector3.UnitZ, new(1, 1)),
					
					// Front
					new BasicVertex(new(-x, -y,  z),  Vector3.UnitZ, new(0, 0)),	//12
					new BasicVertex(new( x, -y,  z),  Vector3.UnitZ, new(1, 0)),
					new BasicVertex(new(-x,  y,  z),  Vector3.UnitZ, new(0, 1)),
					new BasicVertex(new( x,  y,  z),  Vector3.UnitZ, new(1, 1)),

					// Left
					new BasicVertex(new(-x, -y, -z), -Vector3.UnitX, new(0, 0)),	//16
					new BasicVertex(new(-x, -y,  z), -Vector3.UnitX, new(1, 0)),
					new BasicVertex(new(-x,  y, -z), -Vector3.UnitX, new(0, 1)),
					new BasicVertex(new(-x,  y,  z), -Vector3.UnitX, new(1, 1)),

					// Right
					new BasicVertex(new( x, -y, -z),  Vector3.UnitX, new(0, 0)),	//20
					new BasicVertex(new( x, -y,  z),  Vector3.UnitX, new(1, 0)),
					new BasicVertex(new( x,  y, -z),  Vector3.UnitX, new(0, 1)),
					new BasicVertex(new( x,  y,  z),  Vector3.UnitX, new(1, 1)),
				],
				verticesExt = _useExtendedData ?
				[
					// Top
					new ExtendedVertex( Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitZ, Vector2.Zero),

					// Bottom
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),

					// Back
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),

					// Front
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),

					// Left
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitY, Vector2.Zero),

					// Right
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex( Vector3.UnitY, Vector2.Zero),
				] : null,
				indices16 =
				[
					// Top
					0, 2, 1,
					1, 2, 3,

					// Bottom
					4, 5, 6,
					5, 7, 6,

					// Back
					8, 10, 9,
					9, 10, 11,

					// Front
					12, 13, 14,
					13, 15, 14,

					// Left
					16, 17, 18,
					17, 19, 18,

					// Right
					20, 22, 21,
					21, 22, 23,
				],
			};
		}

		public static bool CreateCubeMesh(
			string _resourceKey,
			Engine _engine,
			Vector3 _size,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out StaticMesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _engine == null || _engine.IsDisposed)
			{
				(_engine?.Logger ?? Logger.Instance)?.LogError("Cannot create cube mesh using null resource key or null engine!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreateCubeData(_size, _useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_engine.Logger.LogError("Failed to create cube mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _engine, _useExtendedData, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		/// <summary>
		/// Creates surface geometry data for a simple cube.
		/// </summary>
		/// <param name="_radius">The radius of the cylinder.</param>
		/// <param name="_height">The height of the cylinder, from its base to its upper edge</param>
		/// <param name="_subdivisions">The number of vertices to place along the upper and lower edge of the cylinder. Must be 3 or more.<para/>
		/// HINT: 32 subdivisions appear sufficiently round for most purposes, most people won't notice artifacts beyond that point.</param>
		/// <param name="_useExtendedData">Whether to generate extended vertex data (tangents, secondary UVs) for this mesh.</param>
		public static MeshSurfaceData CreateCylinderData(float _radius, float _height, int _subdivisions, bool _useExtendedData)
		{
			// Geometry counts:
			_subdivisions = Math.Max(_subdivisions, 3);
			int quadCountShell = _subdivisions + 1;
			int triangleCountCap = _subdivisions - 2;

			int vertexCountShell = 2 * quadCountShell;
			int vertexCountCap = _subdivisions;
			int vertexCount = vertexCountShell + 2 * vertexCountCap;

			int indexCountShell = 3 * 2 * quadCountShell;
			int indexCountCap = 3 * triangleCountCap;
			int indexCount = indexCountShell + 2 * indexCountCap;

			// Data arrays:
			BasicVertex[] verticesBasic = new BasicVertex[vertexCount];
			ExtendedVertex[]? verticesExt = _useExtendedData ? new ExtendedVertex[vertexCount] : null;
			ushort[] indices = new ushort[indexCount];

			// Start indices for caps:
			int iStartIdxCapUp = indexCountShell;
			int iStartIdxCapDown = indexCountShell + indexCountCap;
			int vStartIdxCapUp = vertexCountShell;
			int vStartIdxCapDown = vertexCountShell + vertexCountCap;

			// Common math parameters:
			float angleStepRad = 2 * MathF.PI / _subdivisions;
			float uvStep = 1.0f / _subdivisions;
			float y = _height / 2;

			// Populate last 2 shell vertices right away:
			verticesBasic[vertexCountShell - 2] = new(new(_radius, -y, 0), Vector3.UnitX, new(1, 0));
			verticesBasic[vertexCountShell - 1] = new(new(_radius, y, 0), Vector3.UnitX, new(1, 1));
			if (_useExtendedData)
			{
				verticesExt![vertexCountShell - 2] = new(Vector3.UnitZ, new(1, 0));
				verticesExt![vertexCountShell - 1] = new(Vector3.UnitZ, new(1, 1));
			}

			for (int i = 0; i < _subdivisions; ++i)
			{
				// Common math & geometry:
				float angleRad = i * angleStepRad;
				float c = MathF.Cos(angleRad);
				float s = MathF.Sin(angleRad);
				float posX = c * _radius;
				float posZ = s * _radius;
				Vector3 posDown = new(posX, -y, posZ);
				Vector3 posUp = new(posX, y, posZ);

				// Shell quads:
				Vector3 normalShell = new(c, 0, s);
				float uvShellX = i * uvStep;
				Vector2 shellUvDown = new(uvShellX, 0);
				Vector2 shellUvUp = new(uvShellX, 1);

				int vStartIdxQuad = 2 * i;
				verticesBasic[vStartIdxQuad + 0] = new(posDown, normalShell, shellUvDown);
				verticesBasic[vStartIdxQuad + 1] = new(posUp, normalShell, shellUvUp);

				int iStartIdxQuad = 6 * i;
				indices[iStartIdxQuad + 0] = (ushort)(vStartIdxQuad + 0);
				indices[iStartIdxQuad + 1] = (ushort)(vStartIdxQuad + 1);
				indices[iStartIdxQuad + 2] = (ushort)(vStartIdxQuad + 2);

				indices[iStartIdxQuad + 3] = (ushort)(vStartIdxQuad + 1);
				indices[iStartIdxQuad + 4] = (ushort)(vStartIdxQuad + 3);
				indices[iStartIdxQuad + 5] = (ushort)(vStartIdxQuad + 2);

				// Cap fans vertices:
				Vector2 capUvUp = new Vector2(c + 1, s + 1) * 0.5f;
				Vector2 capUvDown = new Vector2(1 - c, 1 - s) * 0.5f;

				int vStartIdxFanUp = vStartIdxCapUp + i;
				int vStartIdxFanDown = vStartIdxCapDown + i;
				verticesBasic[vStartIdxFanUp] = new(posUp, Vector3.UnitY, capUvUp);
				verticesBasic[vStartIdxFanDown] = new(posDown, -Vector3.UnitY, capUvDown);

				// Extended vertex data:
				if (_useExtendedData)
				{
					// Shell:
					Vector3 tagentShell = new(s, 0, c);

					verticesExt![vStartIdxQuad + 0] = new(tagentShell, shellUvDown);
					verticesExt![vStartIdxQuad + 1] = new(tagentShell, shellUvUp);

					// Caps:
					verticesExt![vStartIdxFanUp] = new(Vector3.UnitZ, capUvUp);
					verticesExt![vStartIdxFanDown] = new(-Vector3.UnitZ, capUvDown);
				}
			}

			// Cap fan indices:
			for (int i = 0; i < triangleCountCap; ++i)
			{
				int vCurIdxFanUp = vStartIdxCapUp + i;
				int vCurIdxFanDown = vStartIdxCapDown + i;
				int iStartIdxFanUp = iStartIdxCapUp + 3 * i;
				int iStartIdxFanDown = iStartIdxCapDown + 3 * i;

				// Top:
				indices[iStartIdxFanUp + 0] = (ushort)vStartIdxCapUp;
				indices[iStartIdxFanUp + 1] = (ushort)(vCurIdxFanUp + 2);
				indices[iStartIdxFanUp + 2] = (ushort)(vCurIdxFanUp + 1);

				// Bottom:
				indices[iStartIdxFanDown + 0] = (ushort)vStartIdxCapDown;
				indices[iStartIdxFanDown + 1] = (ushort)(vCurIdxFanDown + 1);
				indices[iStartIdxFanDown + 2] = (ushort)(vCurIdxFanDown + 2);
			}

			return new MeshSurfaceData()
			{
				verticesBasic = verticesBasic,
				verticesExt = verticesExt,
				indices16 = indices,
				indices32 = null,
			};
		}

		public static bool CreateCylinderMesh(
			string _resourceKey,
			Engine _engine,
			float _radius, float _height, int _subdivisions,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out StaticMesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _engine == null || _engine.IsDisposed)
			{
				(_engine?.Logger ?? Logger.Instance)?.LogError("Cannot create cylinder mesh using null resource key or null engine!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreateCylinderData(_radius, _height, _subdivisions, _useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_engine.Logger.LogError("Failed to create cylinder mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _engine, _useExtendedData, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		#endregion
	}
}
