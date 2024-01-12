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

		public static bool UpdateDefaultResourceSet(
			in Material _material,
			in CameraContext _cameraCtx,
			in DeviceBuffer _objectDataConstantBuffer,
			ref ResourceSet? _defaultResourceSet)
		{
			if (_defaultResourceSet == null || _defaultResourceSet.IsDisposed)
			{
				_defaultResourceSet?.Dispose();
				_defaultResourceSet = null;

				ResourceSetDescription resourceSetDesc = new(
					_material.ResourceLayout,
					_cameraCtx.globalConstantBuffer,
					_objectDataConstantBuffer,
					_cameraCtx.lightDataBuffer,
					_cameraCtx.shadowMapArray);

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
