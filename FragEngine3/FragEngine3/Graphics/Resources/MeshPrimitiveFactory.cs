using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using System.Numerics;

namespace FragEngine3.Graphics.Resources
{
	public static class MeshPrimitiveFactory
	{
		#region Methods

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
			ResourceManager _resourceManager,
			GraphicsCore _graphicsCore,
			Vector3 _size,
			bool _useExtendedData,
			out MeshSurfaceData _outMeshData,
			out StaticMesh _outMesh,
			out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey) || _resourceManager == null || _graphicsCore == null)
			{
				(_resourceManager?.engine.Logger ?? Logger.Instance)?.LogError("Cannot create cube mesh using null resource key, resource manager, or graphics core!");
				_outMeshData = null!;
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMeshData = CreateCubeData(_size, _useExtendedData);
			if (!_outMeshData.IsValid)
			{
				_resourceManager.engine.Logger.LogError("Failed to create cube mesh data!");
				_outMesh = null!;
				_outHandle = ResourceHandle.None;
				return false;
			}

			_outMesh = new(_resourceKey, _resourceManager, _graphicsCore, _useExtendedData, out _outHandle);

			return _outMesh.SetGeometry(in _outMeshData);
		}

		#endregion
	}
}
