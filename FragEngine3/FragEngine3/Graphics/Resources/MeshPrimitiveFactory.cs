using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using System.Numerics;

namespace FragEngine3.Graphics.Resources
{
	public static class MeshPrimitiveFactory
	{
		#region Fields

		// Index data array:
		private static readonly ushort[] icoPositionIndices =
		[
			// Ring pointing up:
			0, 5, 1,
			1, 6, 2,
			2, 7, 3,
			3, 8, 4,
			0, 4, 9,

			// Ring pointing down:
			1, 5, 6,
			2, 6, 7,
			3, 7, 8,
			4, 8, 9,
			0, 9, 5,

			// Top:
			5, 11, 6,
			6, 11, 7,
			7, 11, 8,
			8, 11, 9,
			9, 11, 5,

			// Bottom:
			0, 1, 10,
			1, 2, 10,
			2, 3, 10,
			3, 4, 10,
			4, 0, 10,
		];

		#endregion
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
					new BasicVertex(new(-x, -y, -z), -Vector3.UnitY, new(0, 1)),	//4
					new BasicVertex(new( x, -y, -z), -Vector3.UnitY, new(1, 1)),
					new BasicVertex(new(-x, -y,  z), -Vector3.UnitY, new(0, 0)),
					new BasicVertex(new( x, -y,  z), -Vector3.UnitY, new(1, 0)),

					// Back
					new BasicVertex(new(-x, -y, -z), -Vector3.UnitZ, new(0, 0)),	//8
					new BasicVertex(new( x, -y, -z), -Vector3.UnitZ, new(1, 0)),
					new BasicVertex(new(-x,  y, -z), -Vector3.UnitZ, new(0, 1)),
					new BasicVertex(new( x,  y, -z), -Vector3.UnitZ, new(1, 1)),
					
					// Front
					new BasicVertex(new(-x, -y,  z),  Vector3.UnitZ, new(1, 0)),	//12
					new BasicVertex(new( x, -y,  z),  Vector3.UnitZ, new(0, 0)),
					new BasicVertex(new(-x,  y,  z),  Vector3.UnitZ, new(1, 1)),
					new BasicVertex(new( x,  y,  z),  Vector3.UnitZ, new(0, 1)),

					// Left
					new BasicVertex(new(-x, -y, -z), -Vector3.UnitX, new(1, 0)),	//16
					new BasicVertex(new(-x, -y,  z), -Vector3.UnitX, new(0, 0)),
					new BasicVertex(new(-x,  y, -z), -Vector3.UnitX, new(1, 1)),
					new BasicVertex(new(-x,  y,  z), -Vector3.UnitX, new(0, 1)),

					// Right
					new BasicVertex(new( x, -y, -z),  Vector3.UnitX, new(0, 0)),	//20
					new BasicVertex(new( x, -y,  z),  Vector3.UnitX, new(1, 0)),
					new BasicVertex(new( x,  y, -z),  Vector3.UnitX, new(0, 1)),
					new BasicVertex(new( x,  y,  z),  Vector3.UnitX, new(1, 1)),
				],
				verticesExt = _useExtendedData ?
				[
					// Top
					new ExtendedVertex(Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitZ, Vector2.Zero),

					// Bottom
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),
					new ExtendedVertex(-Vector3.UnitZ, Vector2.Zero),

					// Back
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),

					// Front
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),

					// Left
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),

					// Right
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
					new ExtendedVertex(Vector3.UnitY, Vector2.Zero),
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
			out Mesh _outMesh,
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

			_outMesh = new(_resourceKey, _engine, out _outHandle);

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
		public static MeshSurfaceData CreateCylinderData(float _radius, float _height, uint _subdivisions, bool _useExtendedData)
		{
			// Geometry counts:
			_subdivisions = Math.Max(_subdivisions, 3);
			uint quadCountShell = _subdivisions + 1;
			uint triangleCountCap = _subdivisions - 2;

			uint vertexCountShell = 2 * quadCountShell;
			uint vertexCountCap = _subdivisions;
			uint vertexCount = vertexCountShell + 2 * vertexCountCap;

			uint indexCountShell = 3 * 2 * quadCountShell;
			uint indexCountCap = 3 * triangleCountCap;
			uint indexCount = indexCountShell + 2 * indexCountCap;

			// Data arrays:
			BasicVertex[] verticesBasic = new BasicVertex[vertexCount];
			ExtendedVertex[]? verticesExt = _useExtendedData ? new ExtendedVertex[vertexCount] : null;
			ushort[] indices = new ushort[indexCount];

			// Start indices for caps:
			uint iStartIdxCapUp = indexCountShell;
			uint iStartIdxCapDown = indexCountShell + indexCountCap;
			uint vStartIdxCapUp = vertexCountShell;
			uint vStartIdxCapDown = vertexCountShell + vertexCountCap;

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

			for (uint i = 0; i < _subdivisions; ++i)
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

				uint vStartIdxQuad = 2 * i;
				verticesBasic[vStartIdxQuad + 0] = new(posDown, normalShell, shellUvDown);
				verticesBasic[vStartIdxQuad + 1] = new(posUp, normalShell, shellUvUp);

				uint iStartIdxQuad = 6 * i;
				indices[iStartIdxQuad + 0] = (ushort)(vStartIdxQuad + 0);
				indices[iStartIdxQuad + 1] = (ushort)(vStartIdxQuad + 1);
				indices[iStartIdxQuad + 2] = (ushort)(vStartIdxQuad + 2);

				indices[iStartIdxQuad + 3] = (ushort)(vStartIdxQuad + 1);
				indices[iStartIdxQuad + 4] = (ushort)(vStartIdxQuad + 3);
				indices[iStartIdxQuad + 5] = (ushort)(vStartIdxQuad + 2);

				// Cap fans vertices:
				Vector2 capUvUp = new Vector2(1 - c, s + 1) * 0.5f;
				Vector2 capUvDown = new Vector2(1 - c, 1 - s) * 0.5f;

				uint vStartIdxFanUp = vStartIdxCapUp + i;
				uint vStartIdxFanDown = vStartIdxCapDown + i;
				verticesBasic[vStartIdxFanUp] = new(posUp, Vector3.UnitY, capUvUp);
				verticesBasic[vStartIdxFanDown] = new(posDown, -Vector3.UnitY, capUvDown);

				// Extended vertex data:
				if (_useExtendedData)
				{
					// Shell:
					Vector3 tagentShell = new(-s, 0, c);

					verticesExt![vStartIdxQuad + 0] = new(tagentShell, shellUvDown);
					verticesExt![vStartIdxQuad + 1] = new(tagentShell, shellUvUp);

					// Caps:
					verticesExt![vStartIdxFanUp] = new(Vector3.UnitZ, capUvUp);
					verticesExt![vStartIdxFanDown] = new(-Vector3.UnitZ, capUvDown);
				}
			}

			// Cap fan indices:
			for (uint i = 0; i < triangleCountCap; ++i)
			{
				uint vCurIdxFanUp = vStartIdxCapUp + i;
				uint vCurIdxFanDown = vStartIdxCapDown + i;
				uint iStartIdxFanUp = iStartIdxCapUp + 3 * i;
				uint iStartIdxFanDown = iStartIdxCapDown + 3 * i;

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
			float _radius, float _height, uint _subdivisions,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out Mesh _outMesh,
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

			_outMesh = new(_resourceKey, _engine, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		/// <summary>
		/// Creates surface geometry data for a simple plane.
		/// </summary>
		/// <param name="_size">The width and length of the plane.<para/>
		/// NOTE: The mesh is created in the horizontal plane, with no extent along the vertical/Y axis. The size's Y-axis maps to the scene's "forward" axis, which is Z.</param>
		/// <param name="_subdivisions">The number of vertices to insert between the corners along each side. May not be negative.<para/>
		/// Example: 3 subdivisions corresponds to 3+2=5 vertices along each edge of the plane, resulting in 5x5 quads, i.e. 50 triangle faces in total.</param>
		/// <param name="_useExtendedData">Whether to generate extended vertex data (tangents, secondary UVs) for this mesh.</param>
		public static MeshSurfaceData CreatePlaneData(Vector2 _size, uint _subdivisions, bool _useExtendedData)
		{
			// Geometry counts:
			uint vertsPerSide = Math.Max(_subdivisions + 2, 2);
			uint vertexCount = vertsPerSide * vertsPerSide;

			uint quadsPerSide = vertsPerSide - 1;
			uint quadCount = quadsPerSide * quadsPerSide;
			uint indexCount = 6 * quadCount;

			// Data arrays:
			BasicVertex[] verticesBasic = new BasicVertex[vertexCount];
			ExtendedVertex[]? verticesExt = _useExtendedData ? new ExtendedVertex[vertexCount] : null;
			ushort[] indices = new ushort[indexCount];

			// Common math parameters:
			float stepWidth = 1.0f / quadsPerSide;
			Vector2 posOrigin = -0.5f * _size;

			// Generate vertices:
			for (uint z = 0; z < vertsPerSide; ++z)
			{
				float kZ = z * stepWidth;
				float posZ = posOrigin.Y + kZ * _size.Y;

				for (uint x = 0; x < vertsPerSide; ++x)
				{
					float kX = x * stepWidth;
					float posX = posOrigin.X + kX * _size.X;
					Vector2 uv = new(kX, kZ);

					uint vIndex = z * vertsPerSide + x;

					verticesBasic[vIndex] = new(new(posX, 0, posZ), Vector3.UnitY, uv);
					if (_useExtendedData)
					{
						verticesExt![vIndex] = new(Vector3.UnitZ, uv);
					}
				}
			}

			// Generate triangle indices:
			for (uint z = 0; z < quadsPerSide; ++z)
			{
				for (uint x = 0; x < quadsPerSide; ++x)
				{
					uint quadIdx = z * quadsPerSide + x;
					uint iStartIdx = 6 * quadIdx;
					uint vStartIdx = z * vertsPerSide + x;

					indices[iStartIdx + 0] = (ushort)vStartIdx;
					indices[iStartIdx + 1] = (ushort)(vStartIdx + vertsPerSide);
					indices[iStartIdx + 2] = (ushort)(vStartIdx + 1);

					indices[iStartIdx + 3] = (ushort)(vStartIdx + 1);
					indices[iStartIdx + 4] = (ushort)(vStartIdx + vertsPerSide);
					indices[iStartIdx + 5] = (ushort)(vStartIdx + vertsPerSide + 1);
				}
			}

			return new MeshSurfaceData()
			{
				verticesBasic = verticesBasic,
				verticesExt = verticesExt,
				indices16 = indices,
				indices32 = null,
			};
		}

		public static bool CreatePlaneMesh(
			string _resourceKey,
			Engine _engine,
			Vector2 _size, uint _subdivisions,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out Mesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _engine == null || _engine.IsDisposed)
			{
				(_engine?.Logger ?? Logger.Instance)?.LogError("Cannot create plane mesh using null resource key or null engine!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreatePlaneData(_size, _subdivisions, _useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_engine.Logger.LogError("Failed to create plane mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _engine, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		public static MeshSurfaceData CreateConeData(float _radius, float _height, uint _subdivisions, bool _useExtendedData)
		{
			// Geometry counts:
			_radius = Math.Max(_radius, 0.001f);
			_height = Math.Max(_height, 0);
			_subdivisions = Math.Max(_subdivisions, 3);
			uint vertexCountSides = 2 * _subdivisions + 1;	// +1 from tip
			uint vertexCountBase = _subdivisions;
			uint vertexCount = vertexCountSides + vertexCountBase;

			uint triangleCountSides = _subdivisions;
			uint triangleCountBase = _subdivisions - 2;
			uint triangleCount = triangleCountSides + triangleCountBase;
			uint indexCountSides = 3 * triangleCountSides;
			uint indexCount = 3 * triangleCount;

			// Data arrays:
			BasicVertex[] verticesBasic = new BasicVertex[vertexCount];
			ExtendedVertex[]? verticesExt = _useExtendedData ? new ExtendedVertex[vertexCount] : null;
			ushort[] indices = new ushort[indexCount];

			// Common math parameters:
			float angleStepWidth = 2.0f * MathF.PI / _subdivisions;
			float slopeAngleRad = MathF.Atan(_height / _radius);
			float slopeExternalAngle = MathF.PI / 2 - slopeAngleRad;
			float normSideH = MathF.Cos(slopeExternalAngle);
			float normSideV = MathF.Sin(slopeExternalAngle);
			Vector3 posTip = new(0, _height, 0);

			float baseCircumference = 2 * MathF.PI * _radius;
			float slopeLength = MathF.Sqrt(_radius * _radius + _height * _height);
			float uvRelativeWidth = baseCircumference / slopeLength;
			float uvRelativeHeight = 1.0f;
			if (uvRelativeWidth > 1)
			{
				uvRelativeHeight /= uvRelativeWidth;
				uvRelativeWidth = 1;
			}
			float uvStepWidth = uvRelativeWidth / _subdivisions;

			// Generate vertices:
			verticesBasic[vertexCountSides - 1] = new(new(_radius, 0, 0), new(normSideH, normSideV, 0), new(uvRelativeWidth, 0));
			if (_useExtendedData)
			{
				verticesExt![vertexCountSides - 1] = new(Vector3.UnitZ, new(uvRelativeWidth, 0));
			}

			for (uint i = 0; i < _subdivisions; ++i)
			{
				float angleB = i * angleStepWidth;
				float angleT = (i + 0.5f) * angleStepWidth;
				float cB = MathF.Cos(angleB);
				float sB = MathF.Sin(angleB);
				float cT = MathF.Cos(angleT);
				float sT = MathF.Sin(angleT);
				Vector3 posBase = new(cB * _radius, 0, sB * _radius);

				// Vertices sides:
				Vector3 normSideB = new(cB * normSideH, normSideV, sB * normSideH);
				Vector3 normSideT = new(cT * normSideH, normSideV, sT * normSideH);
				Vector2 uvSideB = new(i * uvStepWidth, 0);
				Vector2 uvSideT = new((i + 0.5f) * uvStepWidth, uvRelativeHeight);

				uint vStartIdxSides = 2 * i;
				verticesBasic[vStartIdxSides + 0] = new(posBase, normSideB, uvSideB);
				verticesBasic[vStartIdxSides + 1] = new(posTip, normSideT, uvSideT);

				// Vertices base:
				Vector2 uvBase = new Vector2(1 - cB, 1 - sB) * 0.5f;

				uint vStartIdxBase = vertexCountSides + i;
				verticesBasic[vStartIdxBase + 0] = new(posBase, -Vector3.UnitY, uvBase);

				// Indices base:
				uint iStartIdxSides = 3 * i;
				indices[iStartIdxSides + 0] = (ushort)vStartIdxSides;
				indices[iStartIdxSides + 1] = (ushort)(vStartIdxSides + 1);
				indices[iStartIdxSides + 2] = (ushort)(vStartIdxSides + 2);

				if (_useExtendedData)
				{
					// Sides:
					Vector3 tanSideB = new(-sB, 0, cB);
					Vector3 tanSideT = new(-sT, 0, cT);

					verticesExt![vStartIdxSides + 0] = new(tanSideB, uvSideB);
					verticesExt![vStartIdxSides + 1] = new(tanSideT, uvSideT);

					// Base:
					verticesExt![vStartIdxBase] = new(-Vector3.UnitZ, uvBase);
				}
			}

			// Indices base fan:
			for (uint i = 0; i < triangleCountBase; ++i)
			{
				uint vCurIdxBase = vertexCountSides + i;
				uint iStartIdxBase = indexCountSides + 3 * i;
				
				indices[iStartIdxBase + 0] = (ushort)vertexCountSides;
				indices[iStartIdxBase + 1] = (ushort)(vCurIdxBase + 1);
				indices[iStartIdxBase + 2] = (ushort)(vCurIdxBase + 2);
			}

			return new MeshSurfaceData()
			{
				verticesBasic = verticesBasic,
				verticesExt = verticesExt,
				indices16 = indices,
				indices32 = null,
			};
		}

		public static bool CreateConeMesh(
			string _resourceKey,
			Engine _engine,
			float _radius, float _height, uint _subdivisions,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out Mesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _engine == null || _engine.IsDisposed)
			{
				(_engine?.Logger ?? Logger.Instance)?.LogError("Cannot create cone mesh using null resource key or null engine!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreateConeData(_radius, _height, _subdivisions, _useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_engine.Logger.LogError("Failed to create cone mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _engine, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		public static MeshSurfaceData CreateIcosahedronData(float _radius, bool _useExtendedData)
		{
			// Geometry counts:
			_radius = Math.Max(_radius, 0.0001f);
			const uint triangleCount = 20;
			const uint indexCount = 3 * triangleCount;
			const uint vertexCount = 3 * triangleCount;

			// Common math:
			const float angle5thCircle = 2 * MathF.PI / 5;
			float sideLength;
			float ringHeight;
			{
				Vector2 p0 = new(_radius, 0);
				Vector2 p1 = new(
					p0.X * MathF.Cos(angle5thCircle),
					p0.X * MathF.Sin(angle5thCircle));
				sideLength = Vector2.Distance(p0, p1);
				const float angle10thCircle = angle5thCircle / 2;
				Vector2 p2 = new(
					p0.X * MathF.Cos(angle10thCircle),
					p0.X * MathF.Sin(angle10thCircle));
				float ringDistSq02 = Vector2.DistanceSquared(p0, p2);
				ringHeight = MathF.Sqrt(sideLength * sideLength - ringDistSq02);
			}
			float halfRingHeight = ringHeight / 2;
			float topHeight = halfRingHeight + MathF.Sqrt(sideLength * sideLength - _radius * _radius);

			// Vertex positions:
			Vector3[] vertexPositions = new Vector3[12];
			for (int i = 0; i < 5; ++i)
			{
				float angleL = i * angle5thCircle;
				float angleH = (i + 0.5f) * angle5thCircle;
				float cL = MathF.Cos(angleL);
				float sL = MathF.Sin(angleL);
				float cH = MathF.Cos(angleH);
				float sH = MathF.Sin(angleH);

				vertexPositions[i] = new(cL * _radius, -halfRingHeight, sL * _radius);
				vertexPositions[i + 5] = new(cH * _radius, halfRingHeight, sH * _radius);
			}
			vertexPositions[10] = new(0, -topHeight, 0);
			vertexPositions[11] = new(0, topHeight, 0);

			/* Top:
			 *			11
			 * 
			 * High:
			 *		  9   5
			 *		8		6
			 *			7
			 * 
			 * Low:
			 *			0
			 *		4		1
			 *		  3   2
			 * 
			 * Bottom:
			 *			10
			 */

			// Data arrays:
			BasicVertex[] verticesBasic = new BasicVertex[vertexCount];
			ExtendedVertex[]? verticesExt = _useExtendedData ? new ExtendedVertex[triangleCount] : null;
			ushort[] indices = new ushort[indexCount];

			// Sqrt(L^2 * 3/4)

			for (uint i = 0; i < triangleCount; ++i)
			{
				uint iTriangleStartIdx = 3 * i;
				uint rawIdx0 = icoPositionIndices[iTriangleStartIdx + 0];
				uint rawIdx1 = icoPositionIndices[iTriangleStartIdx + 1];
				uint rawIdx2 = icoPositionIndices[iTriangleStartIdx + 2];

				Vector3 pos0 = vertexPositions[rawIdx0];
				Vector3 pos1 = vertexPositions[rawIdx1];
				Vector3 pos2 = vertexPositions[rawIdx2];
				Vector3 normal = Vector3.Normalize(Vector3.Cross(pos1 - pos0, pos2 - pos0));

				uint uvGridX = i % 5;
				uint uvGridY = i / 5;
				Vector2 uvGridOrigin = 0.2f * new Vector2(uvGridX, uvGridY);
				Vector2 uv0 = uvGridOrigin + new Vector2(0, 0);
				Vector2 uv1 = uvGridOrigin + new Vector2(1, 0) * 0.2f;
				Vector2 uv2 = uvGridOrigin + new Vector2(0.5f, 0.866025f) * 0.2f;

				uint vTriangleStartIdx = iTriangleStartIdx;
				verticesBasic[vTriangleStartIdx + 0] = new(pos0, normal, uv0);
				verticesBasic[vTriangleStartIdx + 1] = new(pos1, normal, uv1);
				verticesBasic[vTriangleStartIdx + 2] = new(pos2, normal, uv2);

				indices[iTriangleStartIdx + 0] = (ushort)vTriangleStartIdx;
				indices[iTriangleStartIdx + 1] = (ushort)(vTriangleStartIdx + 1);
				indices[iTriangleStartIdx + 2] = (ushort)(vTriangleStartIdx + 2);

				if (_useExtendedData)
				{
					Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, pos1 - pos0));
					verticesExt![vTriangleStartIdx + 0] = new(tangent, uv0);
					verticesExt![vTriangleStartIdx + 1] = new(tangent, uv1);
					verticesExt![vTriangleStartIdx + 2] = new(tangent, uv2);
				}
			}

			return new MeshSurfaceData()
			{
				verticesBasic = verticesBasic,
				verticesExt = verticesExt,
				indices16 = indices,
				indices32 = null,
			};
		}

		public static bool CreateIcosahedronMesh(
			string _resourceKey,
			Engine _engine,
			float _radius,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out Mesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _engine == null || _engine.IsDisposed)
			{
				// crit fail on creating d20.
				(_engine?.Logger ?? Logger.Instance)?.LogError("Cannot create icosahedron mesh using null resource key or null engine!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreateIcosahedronData(_radius, _useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_engine.Logger.LogError("Failed to create icosahedron mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _engine, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		public static MeshSurfaceData CreateFullscreenQuadData(bool _useExtendedData)
		{
			return new()
			{
				verticesBasic =
				[
					new BasicVertex(new(-1, -1, 0), new(0, 0, -1), new(0, 0)),
					new BasicVertex(new(1, -1, 0), new(0, 0, -1), new(1, 0)),
					new BasicVertex(new(-1, 1, 0), new(0, 0, -1), new(0, 1)),
					new BasicVertex(new(1, 1, 0), new(0, 0, -1), new(1, 1)),
				],
				verticesExt = _useExtendedData
				? [
					new ExtendedVertex(Vector3.UnitY, new(0, 0)),
					new ExtendedVertex(Vector3.UnitY, new(1, 0)),
					new ExtendedVertex(Vector3.UnitY, new(0, 1)),
					new ExtendedVertex(Vector3.UnitY, new(1, 1)),
				]
				: null,
				indices16 =
				[
					0, 2, 1,
					2, 3, 1,
				],
			};
		}

		public static bool CreateFullscreenQuadMesh(
			string _resourceKey,
			Engine _engine,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out Mesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _engine == null || _engine.IsDisposed)
			{
				// crit fail on creating d20.
				(_engine?.Logger ?? Logger.Instance)?.LogError("Cannot create fullscreen quad mesh using null resource key or null engine!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreateFullscreenQuadData(_useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_engine.Logger.LogError("Failed to create fullscreen quad mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _engine, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		#endregion
	}
}
