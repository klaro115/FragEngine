using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Lighting;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Utility
{
	public static class ShadowMapUtility
	{
		#region Methods

		public static bool UpdateOrCreateShadowMapCameraInstance(
			in GraphicsCore _graphicsCore,
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

				mtxWorld2Clip = Matrix4x4.Identity,
				mtxWorld2Pixel = Matrix4x4.Identity,
			};

			if (_cameraInstance == null || _cameraInstance.IsDisposed)
			{
				CameraOutput outputSettings = new CameraOutput()
				{
					resolutionX = LightConstants.shadowResolution,
					resolutionY = LightConstants.shadowResolution,

					colorFormat = _graphicsCore.DefaultColorTargetPixelFormat,
					depthFormat = _graphicsCore.DefaultShadowMapDepthTargetFormat,
					hasDepth = true,
					hasStencil = false,
				};
				CameraClearing clearingSettings = new CameraClearing()
				{
					clearColor = false,
					clearColorValue = new RgbaFloat(0, 0, 0, 0),

					clearDepth = true,
					clearDepthValue = 1.0f,

					clearStencil = false,
					clearStencilValue = 0x00,
				};

				try
				{
					_cameraInstance = new(_graphicsCore, false)
					{
						OutputSettings = outputSettings,
						ClearingSettings = clearingSettings,
						MtxWorld = Matrix4x4.Identity,
					};
					_cameraInstance.MarkDirty();
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

		public static bool CreateShadowMapArray(
			in GraphicsCore _graphicsCore,
			uint _resolutionX,
			uint _resolutionY,
			uint _arraySize,
			out Texture _outTexShadowMapArray)
		{
			if (_graphicsCore == null || !_graphicsCore.IsInitialized)
			{
				(_graphicsCore?.graphicsSystem.engine.Logger ?? Logger.Instance!).LogError("Cannot create shadow map texture array using null or uninitialized graphics core!");
				_outTexShadowMapArray = null!;
				return false;
			}

			_resolutionX = Math.Max(_resolutionX, 8);
			_resolutionY = Math.Max(_resolutionY, 8);
			_arraySize = Math.Max(_arraySize, 1);

			const int depth = 1;

			// Create a 2D texture array in single-channel 16-bit float format:
			TextureDescription textureArrayDesc = new(
				_resolutionX,
				_resolutionY,
				depth,
				1,
				_arraySize,
				_graphicsCore.DefaultShadowMapDepthTargetFormat,
				TextureUsage.Sampled | TextureUsage.DepthStencil,
				TextureType.Texture2D);

			try
			{
				_outTexShadowMapArray = _graphicsCore.MainFactory.CreateTexture(ref textureArrayDesc);
				if (_graphicsCore is not MacOS.MacGraphicsCore)
				{
					_outTexShadowMapArray.Name = $"TexShadowMapArray_{_resolutionX}x{_resolutionY}_count={_arraySize}";
				}	//^Note: Naming a texture array somehow leads to hard crashes on MacOS.
			}
			catch (Exception ex)
			{
				_graphicsCore?.graphicsSystem.engine.Logger.LogException($"Failed to create shadow map texture array! (Resolution: {_resolutionX}x{_resolutionY}, count={_arraySize})", ex);
				_outTexShadowMapArray = null!;
				return false;
			}

			// First element is always the default/blank texture, with depth set to maximum:
			bool wasCleared = _graphicsCore.DefaultShadowMapDepthTargetFormat switch
			{
				PixelFormat.R16_UNorm =>			_graphicsCore.FillTexture(_outTexShadowMapArray, ushort.MaxValue,	_resolutionX, _resolutionY, depth, 0, 0),
				PixelFormat.R32_Float =>			_graphicsCore.FillTexture(_outTexShadowMapArray, 1.0f,				_resolutionX, _resolutionY, depth, 0, 0),
				PixelFormat.D24_UNorm_S8_UInt =>	_graphicsCore.FillTexture(_outTexShadowMapArray, 0xFFFFFF00u,		_resolutionX, _resolutionY, depth, 0, 0),
				PixelFormat.D32_Float_S8_UInt =>	_graphicsCore.FillTexture(_outTexShadowMapArray, 1.0f,				_resolutionX, _resolutionY, depth, 0, 0),
				_ => false,
			};
			if (!wasCleared)
			{
				_graphicsCore?.graphicsSystem.engine.Logger.LogError("Failed to clear first layer of shadow map texture array to maximum depth!");
			}
			return wasCleared;
		}

		public static bool CreateShadowMatrixBuffer(
			in GraphicsCore _graphicsCore,
			uint _totalCascadeCount,
			ref DeviceBuffer? _bufShadowMatrices)
		{
			const uint matrixByteSize = 16 * sizeof(float);

			_totalCascadeCount = Math.Max(_totalCascadeCount, 1);
			uint totalMatrixCount = 2 * _totalCascadeCount;
			uint totalByteSize = totalMatrixCount * matrixByteSize;

			if (_bufShadowMatrices == null || _bufShadowMatrices.IsDisposed || _bufShadowMatrices.SizeInBytes < totalByteSize)
			{
				_bufShadowMatrices?.Dispose();

				BufferDescription bufferDesc = new(
					totalByteSize,
					BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
					matrixByteSize);

				try
				{
					_bufShadowMatrices = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
					_bufShadowMatrices.Name = $"BufShadowMatrices_Capacity={_totalCascadeCount}";
				}
				catch (Exception ex)
				{
					_graphicsCore.graphicsSystem.engine.Logger.LogException("Failed to create shadow projection matrix buffer!", ex);
					return false;
				}
			}
			return true;
		}

		public static bool CreateShadowSampler(
			in GraphicsCore _graphicsCore,
			out Sampler _outSamplerShadowMaps)
		{
			SamplerDescription samplerDesc = new(
				SamplerAddressMode.Clamp,
				SamplerAddressMode.Clamp,
				SamplerAddressMode.Clamp,
				SamplerFilter.MinLinear_MagLinear_MipPoint,
				null,
				0,
				0,
				uint.MaxValue,
				0,
				SamplerBorderColor.OpaqueWhite);

			if (!_graphicsCore.SamplerManager.GetSampler(samplerDesc, out _outSamplerShadowMaps))
			{
				_graphicsCore.graphicsSystem.engine.Logger.LogError("Failed to create sampler for shadow map texture array!");
				return false;
			}
			_outSamplerShadowMaps.Name = "Sampler_ShadowMaps";
			return true;
		}

		#endregion
	}
}
