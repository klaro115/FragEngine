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

		[Obsolete("CB types replaced")]
		public static bool UpdateGlobalConstantBuffer(
			in Scene _scene,
			in CameraInstance _cameraInstance,
			in Pose _cameraWorldPose,
			in Matrix4x4 _mtxWorld2Clip,
			uint _activeLightCount,
			ref DeviceBuffer? _globalConstantBuffer)
		{
			// Ensure the buffer is allocated:
			if (_globalConstantBuffer == null || _globalConstantBuffer.IsDisposed)
			{
				try
				{
					BufferDescription bufferDesc = new(GlobalConstantBuffer.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

					_globalConstantBuffer = _cameraInstance.graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_globalConstantBuffer.Name = "CBGlobal";
					return true;
				}
				catch (Exception ex)
				{
					_cameraInstance.graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera's global constant buffer!", ex);
					return false;
				}
			}

			Vector3 ambientLightLow = _scene.settings.AmbientLightIntensityLow;
			Vector3 ambientLightMid = _scene.settings.AmbientLightIntensityMid;
			Vector3 ambientLightHigh = _scene.settings.AmbientLightIntensityHigh;

			GlobalConstantBuffer cbData = new()
			{
				// Camera vectors & matrices:
				mtxWorld2Clip = _mtxWorld2Clip,
				cameraPosition = new Vector4(_cameraWorldPose.position, 0),
				cameraDirection = new Vector4(_cameraWorldPose.Forward, 0),

				// Camera parameters:
				resolutionX = _cameraInstance.OutputSettings.resolutionX,
				resolutionY = _cameraInstance.OutputSettings.resolutionY,
				nearClipPlane = _cameraInstance.ProjectionSettings.nearClipPlane,
				farClipPlane = _cameraInstance.ProjectionSettings.farClipPlane,

				// Lighting:
				ambientLightLow = new RgbaFloat(new(ambientLightLow, 0)),
				ambientLightMid = new RgbaFloat(new(ambientLightMid, 0)),
				ambientLightHigh = new RgbaFloat(new(ambientLightHigh, 0)),
				lightCount = _activeLightCount,
				shadowFadeStart = 0.9f,
			};

			_cameraInstance.graphicsCore.Device.UpdateBuffer(_globalConstantBuffer, 0, ref cbData, GlobalConstantBuffer.byteSize);

			return true;
		}

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
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera's global constant buffer!", ex);
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
				try
				{
					BufferDescription bufferDesc = new(CBCamera.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

					_cbCamera = _cameraInstance.graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_cbCamera.Name = CBCamera.NAME_IN_SHADER;
					return true;
				}
				catch (Exception ex)
				{
					_cameraInstance.graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create camera's global constant buffer!", ex);
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

		public static bool UpdateOrCreateShadowMapCameraInstance(
			in GraphicsCore _graphicsCore,
			in Framebuffer _shadowMapFramebuffer,
			in Matrix4x4 _mtxShadowWorld2Clip,
			in Matrix4x4 _mtxShadowWorld2Uv,
			bool _isDirectional,
			float _farClipPlane,
			float _spotAngleRad,
			ref CameraInstance? _cameraInstance)
		{
			CameraProjection projectionSettings = new()
			{
				projectionType = _isDirectional
					? CameraProjectionType.Orthographic
					: CameraProjectionType.Perspective,
				nearClipPlane = 0.01f,
				farClipPlane = _farClipPlane,
				FieldOfViewRadians = _spotAngleRad,
				mirrorY = true,

				mtxWorld2Clip = _mtxShadowWorld2Clip,
				mtxWorld2Pixel = _mtxShadowWorld2Uv,
			};

			if (_cameraInstance == null || _cameraInstance.IsDisposed)
			{
				try
				{
					_cameraInstance = new(_graphicsCore, _shadowMapFramebuffer, false)
					{
						OutputSettings = new CameraOutput()
						{
							resolutionX = 1024,
							resolutionY = 1024,
							
							colorFormat = _graphicsCore.DefaultColorTargetPixelFormat,
							depthFormat = _graphicsCore.DefaultShadowMapDepthTargetFormat,
							hasDepth = true,
							hasStencil = false,
						},
						ClearingSettings = new CameraClearing()
						{
							clearColor = false,
							clearColorValue = new RgbaFloat(0, 0, 0, 0),

							clearDepth = true,
							clearDepthValue = 1.0f,

							clearStencil = false,
							clearStencilValue = 0x00,
						},
						MtxWorld = Matrix4x4.Identity,
					};
				}
				catch (Exception ex)
				{
					_cameraInstance = null!;
					Logger? logger = _graphicsCore?.graphicsSystem.engine.Logger ?? Logger.Instance;
					logger?.LogException("Failed to create camera instance for shadow map rendering!", ex);
					return false;
				}
			}

			_cameraInstance.ProjectionSettings = projectionSettings;
			return true;
		}

		#endregion
	}
}
