using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Cameras
{
	public static class CameraUtility
	{
		#region Methods

		public static bool UpdateConstantBuffer_CBScene(
			in GraphicsCore _graphicsCore,
			in SceneSettings _sceneSettings,
			ref DeviceBuffer? _cbScene)
		{
			// Ensure the buffer is allocated:
			if (_cbScene == null || _cbScene.IsDisposed)
			{
				try
				{
					BufferDescription bufferDesc = new(CBScene.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

					_cbScene = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_cbScene.Name = CBScene.NAME_IN_SHADER;
					return true;
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create scene constant buffer (CBScene)!", ex);
					return false;
				}
			}

			Vector3 ambientLightLow = _sceneSettings.AmbientLightIntensityLow;
			Vector3 ambientLightMid = _sceneSettings.AmbientLightIntensityMid;
			Vector3 ambientLightHigh = _sceneSettings.AmbientLightIntensityHigh;

			CBScene cbData = new()
			{
				// Scene lighting:
				ambientLightLow = new RgbaFloat(new(ambientLightLow, 0)),
				ambientLightMid = new RgbaFloat(new(ambientLightMid, 0)),
				ambientLightHigh = new RgbaFloat(new(ambientLightHigh, 0)),
				shadowFadeStart = 0.9f,
			};

			_graphicsCore.Device.UpdateBuffer(_cbScene, 0, ref cbData, CBScene.byteSize);

			return true;
		}

		public static bool CreateConstantBuffer_CBCamera(
			in GraphicsCore _graphicsCore,
			out DeviceBuffer _outCbCamera)
		{
			try
			{
				BufferDescription bufferDesc = new(CBCamera.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

				_outCbCamera = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
				_outCbCamera.Name = CBCamera.NAME_IN_SHADER;
				return true;
			}
			catch (Exception ex)
			{
				_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera constant buffer (CBCamera)!", ex);
				_outCbCamera = null!;
				return false;
			}
		}

		public static bool UpdateConstantBuffer_CBCamera(
			in CameraInstance _cameraInstance,
			in Pose _cameraWorldPose,
			in Matrix4x4 _mtxWorld2Clip,
			uint _cameraIdx,
			uint _activeLightCount,
			uint _shadowMappedLightCount,
			ref DeviceBuffer? _cbCamera)
		{
			// Ensure the buffer is allocated:
			if (_cbCamera == null || _cbCamera.IsDisposed)
			{
				if (!CreateConstantBuffer_CBCamera(in _cameraInstance.graphicsCore, out _cbCamera))
				{
					return false;
				}
			}

			CBCamera cbData = new()
			{
				// Camera vectors & matrices:
				mtxWorld2Clip = _mtxWorld2Clip,
				cameraPosition = new Vector4(_cameraWorldPose.position, 0),
				cameraDirection = new Vector4(_cameraWorldPose.Forward, 0),

				// Camera parameters:
				cameraIdx = _cameraIdx,
				resolutionX = _cameraInstance.OutputSettings.resolutionX,
				resolutionY = _cameraInstance.OutputSettings.resolutionY,
				nearClipPlane = _cameraInstance.ProjectionSettings.nearClipPlane,
				farClipPlane = _cameraInstance.ProjectionSettings.farClipPlane,

				// Per-camera lighting:
				lightCount = _activeLightCount,
				shadowMappedLightCount = Math.Min(_shadowMappedLightCount, _activeLightCount),
			};

			_cameraInstance.graphicsCore.Device.UpdateBuffer(_cbCamera, 0, ref cbData, CBCamera.packedByteSize);

			return true;
		}

		public static bool VerifyOrCreateLightDataBuffer(
			in GraphicsCore _graphicsCore,
			uint _maxActiveLightCount,
			ref DeviceBuffer? lightDataBuffer,
			ref uint lightDataBufferCapacity)
		{
			_maxActiveLightCount = Math.Max(_maxActiveLightCount, 1);
			uint byteSize = LightSourceData.byteSize * _maxActiveLightCount;

			// Create a new buffer if there is none or if the previous one was too small:
			if (lightDataBuffer == null || lightDataBuffer.IsDisposed || lightDataBufferCapacity < byteSize)
			{
				// Purge any previously allocated buffer:
				lightDataBuffer?.Dispose();
				lightDataBuffer = null;

				try
				{
					BufferDescription bufferDesc = new(
						byteSize,
						BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
						LightSourceData.byteSize);

					lightDataBuffer = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					lightDataBuffer.Name = $"BufLights_Capacity={_maxActiveLightCount}";
					lightDataBufferCapacity = byteSize;
					return true;
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera's light data buffer!", ex);
					lightDataBuffer?.Dispose();
					lightDataBufferCapacity = 0;
					return false;
				}
			}
			return true;
		}

		public static bool UpdateLightDataBuffer(
			in GraphicsCore _graphicsCore,
			in DeviceBuffer _lightDataBuffer,
			in LightSourceData[] _lightData,
			uint _lightDataBufferCapacity,
			uint _maxActiveLightCount)
		{
			uint maxLightCount = Math.Min(_lightDataBufferCapacity, _maxActiveLightCount);
			int copyCount = Math.Min(_lightData.Length, (int)maxLightCount);

			if (copyCount == _lightData.Length)
			{
				_graphicsCore.Device.UpdateBuffer(_lightDataBuffer, 0, _lightData);
			}
			else
			{
				ReadOnlySpan<LightSourceData> lightDataSpan = new(_lightData, 0, copyCount);
				_graphicsCore.Device.UpdateBuffer(_lightDataBuffer, 0, lightDataSpan);
			}
			return true;
		}

		public static bool VerifyOrCreateDefaultCameraResourceLayout(
			in GraphicsCore _graphicsCore,
			ref ResourceLayout? _defaultCameraResLayout)
		{
			if (_defaultCameraResLayout == null || _defaultCameraResLayout.IsDisposed)
			{
				try
				{
					ResourceLayoutDescription resLayoutDesc = new(GraphicsConstants.DEFAULT_CAMERA_RESOURCE_LAYOUT_DESC);

					_defaultCameraResLayout = _graphicsCore.MainFactory.CreateResourceLayout(ref resLayoutDesc);
					_defaultCameraResLayout.Name = "ResLayout_DefaultCamera";
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to crate default camera resource layout!", ex);
					_defaultCameraResLayout = null;
					return false;
				}
			}
			return true;
		}

		public static bool UpdateOrCreateDefaultCameraResourceSet(
			in GraphicsCore _graphicsCore,
			in ResourceLayout _defaultCameraResLayout,
			in DeviceBuffer _cbScene,
			in DeviceBuffer _cbCamera,
			in DeviceBuffer _lightDataBuffer,
			in Texture _texShadowMaps,
			in Sampler _samplerShadowMaps,
			ref ResourceSet? _defaultCameraResSet,
			bool _forceRecreate = false)
		{
			if (_forceRecreate || _defaultCameraResSet == null || _defaultCameraResSet.IsDisposed)
			{
				_defaultCameraResSet?.Dispose();

				try
				{
					ResourceSetDescription resSetDesc = new(
						_defaultCameraResLayout,
						_cbScene,
						_cbCamera,
						_lightDataBuffer,
						_texShadowMaps,
						_samplerShadowMaps);

					_defaultCameraResSet = _graphicsCore.MainFactory.CreateResourceSet(ref resSetDesc);
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to crate default camera resource set!", ex);
					_defaultCameraResSet = null;
					return false;
				}
			}
			return true;
		}

		#endregion
	}
}
