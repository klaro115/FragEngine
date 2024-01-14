using FragEngine3.Graphics.Cameras;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class CameraContext(
		CameraInstance _camera,
		CommandList _cmdList,
		DeviceBuffer _cbCamera,
		Framebuffer _activeFramebuffer,
		DeviceBuffer _lightDataBuffer,
		Texture _texShadowMaps,
		OutputDescription _outputDesc)
	{
		#region Fields

		public readonly CameraInstance camera = _camera ?? throw new ArgumentNullException(nameof(_camera), "Camera instance may not be null!");
		public readonly CommandList cmdList = _cmdList ?? throw new ArgumentNullException(nameof(_cmdList), "Command list may not be null!");
		public readonly DeviceBuffer cbCamera = _cbCamera;
		public readonly Framebuffer activeFramebuffer = _activeFramebuffer;
		public readonly DeviceBuffer lightDataBuffer = _lightDataBuffer;
		public readonly Texture texShadowMaps = _texShadowMaps;
		public readonly OutputDescription outputDesc = _outputDesc;
		public readonly bool mirrorY = _camera.ProjectionSettings.mirrorY;

		#endregion
		#region Properties

		/// <summary>
		/// Gets whether this context is fully assigned and has not yet been disposed.
		/// </summary>
		public bool IsValid =>
			camera != null && !camera.IsDisposed &&
			cmdList != null && !cmdList.IsDisposed &&
			cbCamera != null && !cbCamera.IsDisposed &&
			activeFramebuffer != null && !activeFramebuffer.IsDisposed &&
			lightDataBuffer != null && !lightDataBuffer.IsDisposed &&
			texShadowMaps != null && !texShadowMaps.IsDisposed;

		#endregion
	}
}
