using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
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

        public static bool UpdateObjectResourceSet(
            in Material _material,
            in DeviceBuffer _cbObject,
            ref ResourceSet? _objectResourceSet)
        {
            if (_objectResourceSet == null || _objectResourceSet.IsDisposed)
            {
                _objectResourceSet?.Dispose();
                _objectResourceSet = null;

                ResourceSetDescription resourceSetDesc = new(
                    _material.ObjectResourceLayout,
                    _cbObject);

                try
                {
                    _objectResourceSet = _material.core.MainFactory.CreateResourceSet(ref resourceSetDesc);
                    _objectResourceSet.Name = $"ResSet_Object_{_material.resourceKey}";
                }
                catch (Exception ex)
                {
                    _material.core.graphicsSystem.engine.Logger.LogException($"Failed to recreate object resource set for material '{_material.resourceKey}'!", ex);
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
