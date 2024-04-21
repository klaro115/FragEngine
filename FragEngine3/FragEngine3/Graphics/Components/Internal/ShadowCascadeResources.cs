using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components.Internal;

internal sealed class ShadowCascadeResources(LightInstance _light, uint _shadowCascadeIdx) : IDisposable
{
	#region Constructors

	~ShadowCascadeResources()
	{
		Dispose(false);
	}

	#endregion
	#region Events

	public Action? OnRecreateResSetObjectEvent = null;

	#endregion
	#region Fields

	private readonly GraphicsCore core = _light.core;

	public readonly LightInstance light = _light;
	public readonly uint shadowCascadeIdx = _shadowCascadeIdx;

	private CBCamera shadowCbCameraData = default;
	private DeviceBuffer? shadowCbCamera = null;
	public Matrix4x4 mtxShadowWorld2Clip = Matrix4x4.Identity;
	private ResourceSet? shadowResSetCamera = null;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public uint ShadowMapIdx { get; private set; } = 0;

	public Framebuffer? ShadowMapFrameBuffer { get; private set; } = null;
	public DeviceBuffer? ShadowCbCamera => shadowCbCamera;
	public ResourceSet? ShadowResSetCamera => shadowResSetCamera;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _disposing)
	{
		IsDisposed = true;

		shadowResSetCamera?.Dispose();
		shadowCbCamera?.Dispose();

		if (_disposing)
		{
			ShadowMapFrameBuffer = null;
		}
	}

	public bool UpdateResources(
		in SceneContext _sceneCtx,
		in CameraInstance _cameraInstance,
		in Pose _lightSourceWorldPose,
		uint _shadowMapIdx,
		bool _rebuildResSetCamera,
		bool _texShadowMapsHasChanged,
		out bool _outFramebufferChanged,
		out bool _outCbCameraChanged)
	{
		Logger logger = core.graphicsSystem.engine.Logger;

		// Select framebuffer:
		uint shadowMapArrayIdx = _shadowMapIdx + shadowCascadeIdx;
		if (!_sceneCtx.shadowMapArray.GetFramebuffer(shadowMapArrayIdx, out Framebuffer framebuffer))
		{
			logger.LogError($"Failed to select framebuffer for drawing shadow map {_shadowMapIdx} for cascade {shadowCascadeIdx}!");
			_outCbCameraChanged = false;
			_outFramebufferChanged = true;
			return false;
		}
		_outFramebufferChanged = framebuffer != ShadowMapFrameBuffer;
		_rebuildResSetCamera |= _texShadowMapsHasChanged;
		ShadowMapFrameBuffer = framebuffer;

		// Update or create global constant buffer with scene and camera information for the shaders:
		if (!CameraUtility.UpdateConstantBuffer_CBCamera(
			in _cameraInstance!,
			in _lightSourceWorldPose,
			in mtxShadowWorld2Clip,
			Matrix4x4.Identity,
			ShadowMapIdx,
			0,
			0,
			ref shadowCbCameraData,
			ref shadowCbCamera,
			out _outCbCameraChanged))
		{
			logger.LogError($"Failed to update camera constant buffer for drawing shadow map for cascade {shadowCascadeIdx}!");
			return false;
		}
		_rebuildResSetCamera |= _outCbCameraChanged;

		// Camera's default resource set:
		if (!CameraUtility.UpdateOrCreateCameraResourceSet(
			in core,
			in _sceneCtx,
			in shadowCbCamera!,
			in _sceneCtx.dummyLightDataBuffer!,
			ref shadowResSetCamera,
			out bool recreatedResSetCamera,
			_rebuildResSetCamera))
		{
			logger.LogError($"Failed to allocate or update default camera resource set for shadow cascade {shadowCascadeIdx}!");
			return false;
		}
		if (_outCbCameraChanged)
		{
			OnRecreateResSetObjectEvent?.Invoke();
		}

		return true;
	}

	#endregion
}
