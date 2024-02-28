using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components.Internal;

internal sealed class ShadowCascadeResources(Light _light, uint _shadowCascadeIdx) : IDisposable
{
	#region Constructors

	~ShadowCascadeResources()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	private readonly GraphicsCore core = _light.core;

	public readonly Light light = _light;
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

		ShadowMapFrameBuffer?.Dispose();
		shadowResSetCamera?.Dispose();
		shadowCbCamera?.Dispose();
	}

	public bool UpdateResources(
		in SceneContext _sceneCtx,
		in DeviceBuffer _dummyBufLights,
		in CameraInstance _cameraInstance,
		uint _shadowMapIdx,
		bool _rebuildResSetCamera,
		bool _texShadowMapsHasChanged,
		out bool _outFramebufferChanged,
		out bool _outCbCameraChanged)
	{
		Logger logger = core.graphicsSystem.engine.Logger;

		// Ensure render targets are created and assigned:
		_outFramebufferChanged = false;
		if (_shadowMapIdx != ShadowMapIdx || _texShadowMapsHasChanged || ShadowMapFrameBuffer == null || ShadowMapFrameBuffer.IsDisposed)
		{
			ShadowMapFrameBuffer?.Dispose();
			
			ShadowMapIdx = _shadowMapIdx;

			FramebufferAttachmentDescription depthTargetDesc = new(_sceneCtx.texShadowMaps, _shadowMapIdx, 0);
			FramebufferDescription shadowMapFrameBufferDesc = new(depthTargetDesc, []);

			try
			{
				ShadowMapFrameBuffer = core.MainFactory.CreateFramebuffer(ref shadowMapFrameBufferDesc);
				ShadowMapFrameBuffer.Name = $"Framebuffer_ShadowMap_Layer{ShadowMapIdx}_Cascade{shadowCascadeIdx}";
			}
			catch (Exception ex)
			{
				ShadowMapFrameBuffer?.Dispose();
				ShadowMapFrameBuffer = null;
				logger.LogException($"Failed to create framebuffer for drawing shadow map for cascade {shadowCascadeIdx}!", ex);
				_outCbCameraChanged = false;
				return false;
			}
			_outFramebufferChanged = true;
		}

		// Update or create global constant buffer with scene and camera information for the shaders:
		if (!CameraUtility.UpdateConstantBuffer_CBCamera(
			in _cameraInstance!,
			light.node.WorldTransformation,
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
			in _dummyBufLights!,
			ref shadowResSetCamera,
			_rebuildResSetCamera))
		{
			logger.LogError($"Failed to allocate or update default camera resource set for shadow cascade {shadowCascadeIdx}!");
			return false;
		}

		return true;
	}

	#endregion
}
