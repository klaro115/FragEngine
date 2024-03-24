using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Lighting.Data;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Cameras
{
    public static class CameraUtility
	{
		#region Methods

		public static bool UpdateConstantBuffer_CBScene(		// Called once per scene frame.
			in GraphicsCore _graphicsCore,
			in SceneSettings _sceneSettings,
			ref CBScene _cbSceneData,
			ref DeviceBuffer? _cbScene,
			out bool _outCbSceneChanged)
		{
			_outCbSceneChanged = false;

			// Ensure the buffer is allocated:
			if (_cbScene == null || _cbScene.IsDisposed)
			{
				_outCbSceneChanged = true;

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

			_cbSceneData = new()
			{
				// Scene lighting:
				ambientLightLow = new RgbaFloat(new(ambientLightLow, 0)),
				ambientLightMid = new RgbaFloat(new(ambientLightMid, 0)),
				ambientLightHigh = new RgbaFloat(new(ambientLightHigh, 0)),
				shadowFadeStart = 0.9f,
			};

			_graphicsCore.Device.UpdateBuffer(_cbScene, 0, ref _cbSceneData, CBScene.byteSize);

			return true;
		}

		public static bool UpdateConstantBuffer_CBCamera(		// Called once per camera pass.
			in CameraInstance _cameraInstance,
			in Pose _cameraWorldPose,
			in Matrix4x4 _mtxWorld2Clip,
			in Matrix4x4 _mtxCameraMotion,
			uint _cameraIdx,
			uint _activeLightCount,
			uint _shadowMappedLightCount,
			ref CBCamera _cbCameraData,
			ref DeviceBuffer? _cbCamera,
			out bool _outCbCameraChanged)
		{
			// Ensure the buffer is allocated:
			_outCbCameraChanged = false;
			if (_cbCamera == null || _cbCamera.IsDisposed)
			{
				_outCbCameraChanged = true;

				try
				{
					BufferDescription bufferDesc = new(CBCamera.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

					_cbCamera = _cameraInstance.graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_cbCamera.Name = CBCamera.NAME_IN_SHADER;
					return true;
				}
				catch (Exception ex)
				{
					_cameraInstance.graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera constant buffer (CBCamera)!", ex);
					_cbCamera = null;
					return false;
				}
			}

			_cbCameraData = new()
			{
				// Camera vectors & matrices:
				mtxWorld2Clip = _mtxWorld2Clip,
				cameraPosition = new Vector4(_cameraWorldPose.position, 0),
				cameraDirection = new Vector4(_cameraWorldPose.Forward, 0),
				mtxInvCameraMotion = _mtxCameraMotion,

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

			_cameraInstance.graphicsCore.Device.UpdateBuffer(_cbCamera, 0, ref _cbCameraData, CBCamera.packedByteSize);

			return true;
		}

		[Obsolete($"Replaced by {nameof(LightDataBuffer)}")]
		public static bool CreateOrResizeLightDataBuffer(		// called once per camera frame.
			in GraphicsCore _graphicsCore,
			uint _activeLightCount,
			ref DeviceBuffer? _bufLights,
			out bool _outBufLightsChanged)
		{
			_activeLightCount = Math.Max(_activeLightCount, 1);
			uint byteSize = LightSourceData.byteSize * _activeLightCount;

			// Create a new buffer if there is none or if the previous one was too small:
			if (_bufLights == null || _bufLights.IsDisposed || _bufLights.SizeInBytes < byteSize)
			{
				_outBufLightsChanged = true;

				// Purge any previously allocated buffer:
				_bufLights?.Dispose();
				_bufLights = null;

				try
				{
					BufferDescription bufferDesc = new(
						byteSize,
						BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
						LightSourceData.byteSize);

					_bufLights = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_bufLights.Name = $"BufLights_Capacity={_activeLightCount}";
					return true;
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera's light data buffer!", ex);
					_bufLights?.Dispose();
					return false;
				}
			}
			_outBufLightsChanged = false;
			return true;
		}

		[Obsolete($"Replaced by {nameof(LightDataBuffer)}")]
		public static bool UpdateLightDataBuffer(               // called once per camera frame.
			in GraphicsCore _graphicsCore,
			in DeviceBuffer _bufLights,
			in LightSourceData[] _lightData,
			uint _lightDataBufferCapacity,
			uint _maxActiveLightCount)
		{
			uint maxLightCount = Math.Min(_lightDataBufferCapacity, _maxActiveLightCount);
			int copyCount = Math.Min(_lightData.Length, (int)maxLightCount);

			if (copyCount == _lightData.Length)
			{
				_graphicsCore.Device.UpdateBuffer(_bufLights, 0, _lightData);
			}
			else
			{
				ReadOnlySpan<LightSourceData> lightDataSpan = new(_lightData, 0, copyCount);
				_graphicsCore.Device.UpdateBuffer(_bufLights, 0, lightDataSpan);
			}
			return true;
		}

		public static bool CreateCameraResourceLayout(			// called exactly once on scene initialization.
			in GraphicsCore _graphicsCore,
			out ResourceLayout _resLayoutCamera)
		{
			try
			{
				ResourceLayoutDescription resLayoutDesc = new(GraphicsConstants.DEFAULT_CAMERA_RESOURCE_LAYOUT_DESC);

				_resLayoutCamera = _graphicsCore.MainFactory.CreateResourceLayout(ref resLayoutDesc);
				_resLayoutCamera.Name = "ResLayout_Camera";
				return true;
			}
			catch (Exception ex)
			{
				_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to crate default camera resource layout!", ex);
				_resLayoutCamera = null!;
				return false;
			}
		}

		public static bool UpdateOrCreateCameraResourceSet(		// called once per camera pass.
			in GraphicsCore _graphicsCore,
			in SceneContext _sceneCtx,
			in DeviceBuffer _cbCamera,
			in LightDataBuffer _lightDataBuffer,
			ref ResourceSet? _resSetCamera,
			bool _forceRecreate = false)
		{
			if (_forceRecreate || _resSetCamera == null || _resSetCamera.IsDisposed)
			{
				_resSetCamera?.Dispose();

				try
				{
					ResourceSetDescription resSetDesc = new(
						_sceneCtx.resLayoutCamera,
						_sceneCtx.cbScene,
						_cbCamera,
						_lightDataBuffer.BufLights,
						_sceneCtx.shadowMapArray.TexDepthMapArray,
						_sceneCtx.shadowMapArray.BufShadowMatrices,
						_sceneCtx.shadowMapArray.SamplerShadowMaps);

					_resSetCamera = _graphicsCore.MainFactory.CreateResourceSet(ref resSetDesc);
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to crate default camera resource set!", ex);
					_resSetCamera = null;
					return false;
				}
			}
			return true;
		}

		#endregion
	}
}
