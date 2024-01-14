using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Components.Utility
{
	internal static class MeshRendererUtility
	{
		#region Methods

		[Obsolete("Replaced after CBObject was introduced.")]
		public static bool UpdateObjectDataConstantBuffer(
			in GraphicsCore _core,
			in SceneNode _node,
			float _boundingRadius,
			ref DeviceBuffer? objectDataConstantBuffer,
			CommandList _cmdList)
		{
			if (objectDataConstantBuffer == null || objectDataConstantBuffer.IsDisposed)
			{
				BufferDescription constantBufferDesc = new(ObjectDataConstantBuffer.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

				try
				{
					objectDataConstantBuffer = _core.MainFactory.CreateBuffer(ref constantBufferDesc);
					objectDataConstantBuffer.Name = "CBObject";
				}
				catch (Exception ex)
				{
					_core.graphicsSystem.engine.Logger.LogException($"Failed to recreate object data constant buffer for renderer of node '{_node.Name}'!", ex);
					return false;
				}
			}

			Pose worldPose = _node.WorldTransformation;

			ObjectDataConstantBuffer objectData = new()
			{
				mtxLocal2World = worldPose.Matrix,
				worldPosition = worldPose.position,
				boundingRadius = _boundingRadius,
			};

			_cmdList.UpdateBuffer(objectDataConstantBuffer, 0, ref objectData, ObjectDataConstantBuffer.byteSize);

			return true;
		}

		public static bool UpdateConstantBuffer_CBObject(
			in GraphicsCore _core,
			in SceneNode _node,
			float _boundingRadius,
			ref DeviceBuffer? _cbObject,
			CommandList _cmdList)
		{
			if (_cbObject == null || _cbObject.IsDisposed)
			{
				BufferDescription constantBufferDesc = new(CBObject.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

				try
				{
					_cbObject = _core.MainFactory.CreateBuffer(ref constantBufferDesc);
					_cbObject.Name = CBObject.NAME_IN_SHADER;
				}
				catch (Exception ex)
				{
					_core.graphicsSystem.engine.Logger.LogException($"Failed to recreate object data constant buffer for renderer of node '{_node.Name}'!", ex);
					return false;
				}
			}

			Pose worldPose = _node.WorldTransformation;

			CBObject objectData = new()
			{
				mtxLocal2World = worldPose.Matrix,
				worldPosition = worldPose.position,
				boundingRadius = _boundingRadius,
			};

			_cmdList.UpdateBuffer(_cbObject, 0, ref objectData, CBObject.byteSize);

			return true;
		}

		public static bool UpdateDefaultResourceSet(
			in Material _material,
			in SceneContext _sceneCtx,
			in CameraContext _cameraCtx,
			in DeviceBuffer _cbObject,
			ref ResourceSet? _defaultResourceSet)
		{
			if (_defaultResourceSet == null || _defaultResourceSet.IsDisposed)
			{
				_defaultResourceSet?.Dispose();
				_defaultResourceSet = null;

				ResourceSetDescription resourceSetDesc = new(
					_material.ResourceLayout,
					_sceneCtx.cbScene,
					_cameraCtx.cbCamera,
					_cbObject,
					_cameraCtx.lightDataBuffer,
					_cameraCtx.texShadowMaps);

				try
				{
					_defaultResourceSet = _material.core.MainFactory.CreateResourceSet(ref resourceSetDesc);
					_defaultResourceSet.Name = $"ResSet_Default_{_material.resourceKey}";
				}
				catch (Exception ex)
				{
					_material.core.graphicsSystem.engine.Logger.LogException($"Failed to recreate default resource set for material '{_material.resourceKey}'!", ex);
					return false;
				}
			}
			return true;
		}

		#endregion
	}
}
