//using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting.Internal;
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
			if (_cbScene is null || _cbScene.IsDisposed)
			{
				_outCbSceneChanged = true;

				if (!CreateConstantBuffer_CBScene(_graphicsCore, out _cbScene))
				{
					return false;
				}
			}

			//TimeManager timeManager = _graphicsCore.graphicsSystem.Engine.TimeManager;

			_cbSceneData = new()
			{
				// Scene lighting:
				ambientLightLow = _sceneSettings.AmbientLightIntensityLow,
				ambientLightMid = _sceneSettings.AmbientLightIntensityMid,
				ambientLightHigh = _sceneSettings.AmbientLightIntensityHigh,
				shadowFadeStart = 0.9f,

				// Time:
				/*
				shaderTime = timeManager.ShaderTimeSeconds,
				deltaTime = (float)timeManager.DeltaTime.TotalSeconds,
				frameCounter = (uint)(timeManager.FrameCount & 0xFFFFFFFFL),
				sinTime = MathF.Sin(engineRunTime),
				*/
			};

			_graphicsCore.Device.UpdateBuffer(_cbScene, 0, ref _cbSceneData, CBScene.byteSize);

			return true;
		}

		public static bool CreateConstantBuffer_CBScene(GraphicsCore _graphicsCore, out DeviceBuffer? _outCbScene)
		{
			try
			{
				BufferDescription bufferDesc = new(CBScene.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

				_outCbScene = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
				_outCbScene.Name = CBScene.NAME_IN_SHADER;
				return true;
			}
			catch (Exception ex)
			{
				_graphicsCore.graphicsSystem.Engine.Logger.LogException("Failed to create scene constant buffer (CBScene)!", ex);
				_outCbScene = null;
				return false;
			}
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
			if (_cbCamera is null || _cbCamera.IsDisposed)
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
					_cameraInstance.graphicsCore.graphicsSystem.Engine.Logger.LogException("Failed to create camera constant buffer (CBCamera)!", ex);
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
				_graphicsCore.graphicsSystem.Engine.Logger.LogException("Failed to crate default camera resource layout!", ex);
				_resLayoutCamera = null!;
				return false;
			}
		}

		public static bool UpdateOrCreateCameraResourceSet(     // called once per camera pass.
			in GraphicsCore _graphicsCore,
			in SceneContext _sceneCtx,
			in DeviceBuffer _cbCamera,
			in LightDataBuffer _lightDataBuffer,
			ref ResourceSet? _resSetCamera,
			out bool _outRecreatedResSetObject,
			bool _forceRecreate = false)
		{
			_outRecreatedResSetObject = false;

			if (_forceRecreate || _resSetCamera is null || _resSetCamera.IsDisposed)
			{
				_resSetCamera?.Dispose();

				try
				{
					ResourceSetDescription resSetDesc = new(
						_sceneCtx.ResLayoutCamera,
						_sceneCtx.CbScene,
						_cbCamera,
						_lightDataBuffer.BufLights,
						_sceneCtx.ShadowMapArray.TexDepthMapArray,
						_sceneCtx.ShadowMapArray.TexNormalMapArray,
						_sceneCtx.ShadowMapArray.BufShadowMatrices,
						_sceneCtx.ShadowMapArray.SamplerShadowMaps);

					_resSetCamera = _graphicsCore.MainFactory.CreateResourceSet(ref resSetDesc);
					_resSetCamera.Name = $"ResSetCamera";

					_outRecreatedResSetObject = true;
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.Engine.Logger.LogException("Failed to crate default camera resource set!", ex);
					_resSetCamera = null;
					return false;
				}
			}
			return true;
		}

		#endregion
	}
}
