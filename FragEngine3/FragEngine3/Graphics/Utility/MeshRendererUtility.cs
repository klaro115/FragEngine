using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Utility
{
	internal static class MeshRendererUtility
	{
		#region Methods

		public static bool UpdateConstantBuffer_CBObject(
			in GraphicsCore _core,
			in SceneNode _node,
			float _boundingRadius,
			ref CBObject _cbObjectData,
			ref DeviceBuffer? _cbObject,
			out bool _outCbObjectChanged)
		{
			_outCbObjectChanged = false;
			if (_cbObject == null || _cbObject.IsDisposed)
			{
				_outCbObjectChanged = true;

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

			_cbObjectData = new()
			{
				mtxLocal2World = worldPose.Matrix,
				worldPosition = worldPose.position,
				boundingRadius = _boundingRadius,
			};

			_core.Device.UpdateBuffer(_cbObject, 0, ref _cbObjectData, CBObject.byteSize);

			return true;
		}

		public static bool UpdateObjectResourceSet(
			in GraphicsCore _graphicsCore,
			in ResourceLayout _resLayoutObject,
			in DeviceBuffer _cbObject,
			string _objectName,
			ref ResourceSet? _objectResourceSet,
			bool _forceRecreate = false)
		{
			if (_forceRecreate || _objectResourceSet == null || _objectResourceSet.IsDisposed)
			{
				_objectResourceSet?.Dispose();
				_objectResourceSet = null;

				ResourceSetDescription resourceSetDesc = new(
					_resLayoutObject,
					_cbObject);

				try
				{
					_objectResourceSet = _graphicsCore.MainFactory.CreateResourceSet(ref resourceSetDesc);
					_objectResourceSet.Name = $"ResSet_Object_{_objectName}";
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException($"Failed to recreate default object resource set for object '{_objectName}'!", ex);
					return false;
				}
			}
			return true;
		}

		#endregion
	}
}
