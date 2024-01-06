using FragEngine3.Containers;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Utility.Serialization;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
    public sealed class StaticMeshRenderer(SceneNode _node) : Component(_node), IRenderer
	{
		#region Fields

		public readonly GraphicsCore core = _node.scene.engine.GraphicsSystem.graphicsCore ?? throw new NullReferenceException("Could not find graphics core for static mesh renderer!");

		private uint rendererVersion = 1;

		private DeviceBuffer? objectDataConstantBuffer = null;
		private VersionedMember<ResourceSet?> defaultResourceSet = new(null, 0);
		private VersionedMember<Pipeline> pipeline = new(null!, 0);

		private ResourceSet? overrideBoundResourceSet = null;

		#endregion
		#region Properties

		public bool IsVisible => !IsDisposed && node.IsEnabled && Mesh != null && !Mesh.IsDisposed;
		public bool DontDrawUnlessFullyLoaded { get; set; } = false;

		public RenderMode RenderMode => Material != null ? Material.RenderMode : RenderMode.Opaque;
		public uint LayerFlags { get; set; } = 1;

		public GraphicsCore GraphicsCore => core;

		/// <summary>
		/// Gets a handle to the material resource that is used to draw this renderer's mesh. A material provides shaders, texture, and lighting instructions.
		/// </summary>
		public ResourceHandle? MaterialHandle { get; private set; } = null;
		public Material? Material { get; private set; } = null;

		/// <summary>
		/// Gets a handle to mesh resource that is drawn by this renderer. A mesh provides the surface geometry of a 3D model.
		/// </summary>
		public ResourceHandle? MeshHandle { get; private set; } = null;
		public StaticMesh? Mesh { get; private set; } = null;
		/// <summary>
		/// Gets the bounding sphere radius enclosing the renderer's mesh.
		/// </summary>
		public float BoundingRadius { get; private set; } = 0.0f;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			base.Dispose(_disposing);

			objectDataConstantBuffer?.Dispose();
			objectDataConstantBuffer = null;

			pipeline.DisposeValue();

			if (_disposing)
			{
				MeshHandle = null;
				Mesh = null;
				MaterialHandle = null;
				Material = null;
			}
		}

		public bool SetMesh(string _resourceKey, bool _loadImmediatelyIfNotReady = false)
		{
			if (string.IsNullOrEmpty(_resourceKey))
			{
				Logger.LogError("Cannot assign mesh to static mesh renderer using null or blank resource key!");
				return false;
			}

			ResourceManager resourceManager = core.graphicsSystem.engine.ResourceManager;

			return resourceManager.GetResource(_resourceKey, out ResourceHandle handle) && SetMesh(handle, _loadImmediatelyIfNotReady);
		}

		public bool SetMesh(ResourceHandle _meshHandle, bool _loadImmediatelyIfNotReady = false)
		{
			if (_meshHandle == null || !_meshHandle.IsValid)
			{
				Logger.LogError("Cannot assign null or disposed mesh handle to static mesh renderer!");
				return false;
			}
			if (_meshHandle.resourceType != ResourceType.Model)
			{
				Logger.LogError($"Cannot assign resource handle of invalid type '{_meshHandle.resourceType}' as mesh to static mesh renderer!");
				return false;
			}

			// If the mesh is already loaded, assign it right away:
			if (_meshHandle.GetResource(_loadImmediatelyIfNotReady) is StaticMesh mesh && !mesh.IsDisposed)
			{
				Mesh = mesh;
				BoundingRadius = mesh.BoundingRadius;
			}
			else
			{
				Mesh = null;
				BoundingRadius = 0.0f;
			}

			// Assign handle:
			MeshHandle = _meshHandle;
			rendererVersion++;
			return true;
		}

		public bool SetMesh(StaticMesh _mesh)
		{
			if (_mesh == null || _mesh.IsDisposed)
			{
				Logger.LogError("Cannot assign null or disposed mesh to static mesh renderer!");
				return false;
			}

			// Check if there is a resource handle to go with the given mesh:
			MeshHandle = null;
			if (!string.IsNullOrEmpty(_mesh.resourceKey))
			{
				ResourceManager resourceManager = core.graphicsSystem.engine.ResourceManager;
				if (resourceManager.GetResource(_mesh.resourceKey, out ResourceHandle handle) && handle.resourceType == ResourceType.Model)
				{
					MeshHandle = handle;
				}
			}

			// Assign mesh and update bounds:
			Mesh = _mesh;
			BoundingRadius = Mesh.BoundingRadius;
			rendererVersion++;
			return true;
		}

		public bool SetMaterial(string _resourceKey, bool _loadImmediatelyIfNotReady = false)
		{
			if (string.IsNullOrEmpty(_resourceKey))
			{
				Logger.LogError("Cannot assign material to static mesh renderer using null or blank resource key!");
				return false;
			}

			ResourceManager resourceManager = core.graphicsSystem.engine.ResourceManager;

			return resourceManager.GetResource(_resourceKey, out ResourceHandle handle) && SetMaterial(handle, _loadImmediatelyIfNotReady);
		}

		public bool SetMaterial(ResourceHandle _materialHandle, bool _loadImmediatelyIfNotReady = false)
		{
			if (_materialHandle == null || !_materialHandle.IsValid)
			{
				Logger.LogError("Cannot assign null or disposed material handle to static mesh renderer!");
				return false;
			}
			if (_materialHandle.resourceType != ResourceType.Material)
			{
				Logger.LogError($"Cannot assign resource handle of invalid type '{_materialHandle.resourceType}' as material to static mesh renderer!");
				return false;
			}

			// If the material is already loaded, assign it right away:
			if (_materialHandle.GetResource(_loadImmediatelyIfNotReady) is Material material && !material.IsDisposed)
			{
				Material = material;
			}
			else
			{
				Material = null;
			}

			// Assign handle:
			MaterialHandle = _materialHandle;
			rendererVersion++;
			return true;
		}

		public bool SetMaterial(Material _material)
		{
			if (_material == null || _material.IsDisposed)
			{
				Logger.LogError("Cannot assign null or disposed material to static mesh renderer!");
				return false;
			}

			// Check if there is a resource handle to go with the given material:
			MaterialHandle = null;
			if (!string.IsNullOrEmpty(_material.resourceKey))
			{
				ResourceManager resourceManager = core.graphicsSystem.engine.ResourceManager;
				if (resourceManager.GetResource(_material.resourceKey, out ResourceHandle handle) && handle.resourceType == ResourceType.Material)
				{
					MaterialHandle = handle;
				}
			}

			// Assign material:
			Material = _material;
			rendererVersion++;
			return true;
		}

		public bool SetOverrideBoundResourceSet(ResourceSet? _newOverrideResourceSet)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot set override resource set on disposed static mesh renderer!");
				return false;
			}

			if (_newOverrideResourceSet == null || _newOverrideResourceSet.IsDisposed)
			{
				overrideBoundResourceSet = null;
			}
			else
			{
				overrideBoundResourceSet = _newOverrideResourceSet;
			}
			return true;
		}

		public float GetZSortingDepth(Vector3 _viewportPosition, Vector3 _cameraDirection)
		{
			float zSortingBias = Material != null ? Material.ZSortingBias : 0.0f;
			Vector3 posNode = node.WorldPosition;
			Vector3 posFront = posNode - _viewportPosition * (BoundingRadius - zSortingBias);

			return Vector3.DistanceSquared(posFront, _viewportPosition);
		}

		public bool Draw(CameraContext _cameraCtx)
		{
			// Check mesh and load it now if necessary:
			if (Mesh == null || Mesh.IsDisposed)
			{
				if (MeshHandle == null || !MeshHandle.IsValid)
				{
					return false;
				}
				// Abort drawing until mesh is ready, queue it up for background loading:
				if (DontDrawUnlessFullyLoaded && !MeshHandle.IsLoaded)
				{
					if (MeshHandle.LoadState == ResourceLoadState.NotLoaded) MeshHandle.Load(false);
					return true;
				}

				if (MeshHandle.GetResource(true, true) is not StaticMesh mesh || !mesh.IsInitialized)
				{
					Logger.LogError($"Failed to load static mesh resource from handle '{MeshHandle}'!");
					return false;
				}
				Mesh = mesh;
				BoundingRadius = Mesh.BoundingRadius;
			}
			// Check material and load it now if necessary:
			if (Material == null || Material.IsDisposed)
			{
				if (MaterialHandle == null || !MaterialHandle.IsValid)
				{
					return false;
				}
				// Abort drawing until material is ready, queue it up for background loading:
				if (DontDrawUnlessFullyLoaded && !MaterialHandle.IsLoaded)
				{
					if (MaterialHandle.LoadState == ResourceLoadState.NotLoaded) MaterialHandle.Load(false);
					return true;
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

			UpdateObjectDataConstantBuffer(_cameraCtx.cmdList);

			// Update (or recreate) pipeline for rendering this material and geometry combo:
			if (!Material.IsPipelineUpToDate(in pipeline, rendererVersion))
			{
				pipeline.DisposeValue();
				if (!Material.CreatePipeline(_cameraCtx, rendererVersion, vertexDataFlags, out pipeline))
				{
					Logger.LogError($"Failed to retrieve pipeline description for material '{Material}'!");
					return false;
				}
			}

			UpdateResourceSet(Material, _cameraCtx);

			// Throw pipeline and geometry buffers at the command list:
			_cameraCtx.cmdList.SetPipeline(pipeline.Value);
			_cameraCtx.cmdList.SetGraphicsResourceSet(0, defaultResourceSet.Value);

			ResourceSet? boundResourceSet = overrideBoundResourceSet ?? Material.BoundResourceSet;
			if (boundResourceSet != null && Material.BoundResourceLayout != null)
			{
				_cameraCtx.cmdList.SetGraphicsResourceSet(1, boundResourceSet);
			}

			for (uint i = 0; i < vertexBuffers.Length; ++i)
			{
				_cameraCtx.cmdList.SetVertexBuffer(i, vertexBuffers[i]);
			}
			_cameraCtx.cmdList.SetIndexBuffer(indexBuffer, Mesh.IndexFormat);

			// Issue draw call:
			_cameraCtx.cmdList.DrawIndexed(Mesh.IndexCount);

			return true;
		}

		private bool UpdateObjectDataConstantBuffer(CommandList _cmdList)
		{
			if (objectDataConstantBuffer == null || objectDataConstantBuffer.IsDisposed)
			{
				BufferDescription constantBufferDesc = new(ObjectDataConstantBuffer.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

				objectDataConstantBuffer = core.MainFactory.CreateBuffer(ref constantBufferDesc);
				objectDataConstantBuffer.Name = "CBObject";
				rendererVersion++;
			}

			Pose worldPose = node.WorldTransformation;
			//Pose worldPose = node.LocalTransformation;

			ObjectDataConstantBuffer objectData = new()
			{
				mtxWorld = worldPose.Matrix,
				worldPosition = worldPose.position,
				boundingRadius = BoundingRadius,
			};

			_cmdList.UpdateBuffer(objectDataConstantBuffer, 0, ref objectData, ObjectDataConstantBuffer.byteSize);

			return true;
		}

		private bool UpdateResourceSet(Material _material, CameraContext _cameraCtx)
		{
			if (!defaultResourceSet.GetValue(rendererVersion, out ResourceSet? rs) || rs == null || rs.IsDisposed)
			{
				defaultResourceSet.DisposeValue();

				ResourceSetDescription resourceSetDesc = new(
					_material.ResourceLayout,
					_cameraCtx.globalConstantBuffer,
					objectDataConstantBuffer,
					_cameraCtx.lightDataBuffer);

				rs = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
				rs.Name = $"ResSet_Default_{_material.resourceKey}";
				defaultResourceSet.UpdateValue(rendererVersion, rs);
			}
			
			return true;
		}

		public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
		{
			if (string.IsNullOrEmpty(_componentData.SerializedData))
			{
				Logger.LogError("Cannot load static mesh renderer from null or blank serialized data!");
				return false;
			}

			// Deserialize renderer data from component data's serialized data string:
			if (!Serializer.DeserializeFromJson(_componentData.SerializedData, out StaticMeshRendererData? data) || data == null)
			{
				Logger.LogError("Failed to deserialize static mesh renderer component data from JSON!");
				return false;
			}

			ResourceManager resourceManager = core.graphicsSystem.engine.ResourceManager;

			// Reset all resource references:
			MaterialHandle = null;
			Material = null;
			MeshHandle = null;
			Mesh = null;

			DontDrawUnlessFullyLoaded = data.DontDrawUnlessFullyLoaded;
			LayerFlags = data.LayerFlags;

			// Load resource handles and queue up loading if they're not available yet:
			if (!string.IsNullOrEmpty(data.Material))
			{
				if (!resourceManager.GetResource(data.Material, out ResourceHandle handle) || handle.resourceType != ResourceType.Material)
				{
					Logger.LogError($"A material resource with the key '{data.Material}' could not be found!");
					return false;
				}
				MaterialHandle = handle;
				Material = handle.GetResource(false) as Material;
			}
			if (!string.IsNullOrEmpty(data.Mesh))
			{
				if (!resourceManager.GetResource(data.Mesh, out ResourceHandle handle) || handle.resourceType != ResourceType.Model)
				{
					Logger.LogError($"A static mesh resource with the key '{data.Mesh}' could not be found!");
					return false;
				}
				MeshHandle = handle;
				Mesh = handle.GetResource(false) as StaticMesh;
			}
			return true;
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			StaticMeshRendererData data = new()
			{
				Mesh = MeshHandle?.resourceKey ?? Mesh?.resourceKey ?? string.Empty,
				Material = MaterialHandle?.resourceKey ?? Material?.resourceKey ?? string.Empty,

				DontDrawUnlessFullyLoaded = DontDrawUnlessFullyLoaded,
				LayerFlags = LayerFlags,
			};

			if (!Serializer.SerializeToJson(data, out string dataJson))
			{
				Logger.LogError("Failed to serialize static mesh renderer component data to JSON!");
				_componentData = ComponentData.Empty;
				return false;
			}

			_componentData = new ComponentData()
			{
				SerializedData = dataJson,
			};
			return true;
		}

		#endregion
	}
}
