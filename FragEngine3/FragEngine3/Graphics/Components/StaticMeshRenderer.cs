using FragEngine3.Graphics.Resources;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
	public sealed class StaticMeshRenderer : Component, IRenderer
	{
		#region Constructors

		public StaticMeshRenderer(SceneNode _node) : base(_node)
		{
			core = _node.scene.engine.GraphicsSystem.graphicsCore ?? throw new NullReferenceException("Could not find graphics core for static mesh renderer!");
		}

		#endregion
		#region Fields

		public readonly GraphicsCore core;

		#endregion
		#region Properties

		public bool IsVisible => !IsDisposed && node.IsEnabled && Mesh != null && !Mesh.IsDisposed;

		public RenderMode RenderMode => Material != null ? Material.RenderMode : RenderMode.Opaque;

		public GraphicsCore GraphicsCore => throw new NotImplementedException();

		public ResourceHandle? MaterialHandle { get; private set; } = null;
		public Material? Material { get; private set; } = null;

		public ResourceHandle? MeshHandle { get; private set; } = null;
		public StaticMesh? Mesh { get; private set; } = null;
		public float BoundingRadius { get; private set; } = 0.0f;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			base.Dispose(_disposing);

			if (_disposing)
			{
				MeshHandle = null;
				Mesh = null;
				MaterialHandle = null;
				Material = null;
			}
		}

		public bool SetMesh(ResourceHandle _meshHandle)
		{
			if (_meshHandle == null || _meshHandle.IsValid)
			{
				Logger.LogError("Cannot assign null or disposed mesh handle to mesh renderer!");
				return false;
			}

			// If the mesh is already loaded, assign it right away:
			if (_meshHandle.IsLoaded && _meshHandle.GetResource() is StaticMesh mesh && !mesh.IsDisposed)
			{
				if (!SetMesh(mesh))
				{
					return false;
				}
			}

			MeshHandle = _meshHandle;
			return true;
		}

		public bool SetMesh(StaticMesh _mesh)
		{
			if (_mesh == null || _mesh.IsDisposed)
			{
				Logger.LogError("Cannot assign null or disposed mesh to mesh renderer!");
				return false;
			}

			MeshHandle = null;
			Mesh = _mesh;
			BoundingRadius = Mesh.BoundingRadius;
			return true;
		}

		public float GetZSortingDepth(Vector3 _viewportPosition, Vector3 _cameraDirection)
		{
			float zSortingBias = Material != null ? Material.ZSortingBias : 0.0f;
			Vector3 posNode = node.WorldPosition;
			Vector3 posFront = posNode - _viewportPosition * (BoundingRadius - zSortingBias);

			return Vector3.DistanceSquared(posFront, _viewportPosition);
		}

		public bool Draw(CommandList _cmdList)
		{
			// Check mesh and load it now if necessary:
			if (Mesh == null || Mesh.IsDisposed)
			{
				if (MeshHandle == null || !MeshHandle.IsValid)
				{
					return false;
				}
				if (MeshHandle.GetResource(true, true) is not StaticMesh mesh || !mesh.IsInitialized)
				{
					Logger.LogError($"Failed to load static mesh resource from handle '{MeshHandle}'!");
					return false;
				}
				Mesh = mesh;
			}
			// Check material and load it now if necessary:
			if (Material == null || Material.IsDisposed)
			{
				if (MaterialHandle == null || !MaterialHandle.IsValid)
				{
					return false;
				}
				if (MaterialHandle.GetResource(true, true) is not Material material || !material.IsLoaded)
				{
					Logger.LogError($"Failed to load material resource from handle '{MaterialHandle}'!");
					return false;
				}
				Material = material;
			}

			// Fetch geometry buffers:
			if (!Mesh.GetGeometryBuffers(out DeviceBuffer[] vertexBuffers, out DeviceBuffer indexBuffer, out MeshVertexDataFlags vertexDataFlags))
			{
				Logger.LogError($"Failed to retrieve geometry buffers for static mesh '{Mesh}'!");
				return false;
			}

			// Fetch (or create) pipeline description for rendering this material and vertex data combo:
			if (!Material.GetOrUpdatePipeline(out Pipeline pipeline, vertexDataFlags))
			{
				Logger.LogError($"Failed to retrieve pipeline description for material '{Material}'!");
				return false;
			}

			// Throw pipeline and geometry buffers at the command list:
			_cmdList.SetPipeline(pipeline);
			for (uint i = 0; i < vertexBuffers.Length; ++i)
			{
				_cmdList.SetVertexBuffer(i, vertexBuffers[i]);
			}
			_cmdList.SetIndexBuffer(indexBuffer, Mesh.IndexFormat);

			//TODO: Bind material resources, such as textures and buffers!
			//TODO: Update constant buffers, both for system variables and from material!

			// Issue draw call:
			_cmdList.DrawIndexed(Mesh.IndexCount);

			return true;
		}

		public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElementData> _idDataMap)
		{
			//TODO: Add data type for JSON-serializing material and mesh dependencies of this renderer, then load that from component data.
			throw new NotImplementedException();
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			//TODO: Add data type for JSON-serializing material and mesh dependencies of this renderer, then save that to component data.
			throw new NotImplementedException();
		}

		#endregion
	}
}
