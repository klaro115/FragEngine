using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Lighting;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Utility;

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

	#endregion
}
