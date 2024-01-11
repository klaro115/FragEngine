using FragEngine3.Graphics.Cameras;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class CameraContext(
		CameraInstance _camera,
		CommandList _cmdList,
		DeviceBuffer _globalConstantBuffer,
		DeviceBuffer _lightDataBuffer,
		Texture _shadowMapArray,
		OutputDescription _outputDesc)
	{
		#region Fields

		public readonly CameraInstance camera = _camera ?? throw new ArgumentNullException(nameof(_camera), "Camera instance may not be null!");
		public readonly CommandList cmdList = _cmdList;
		public readonly DeviceBuffer globalConstantBuffer = _globalConstantBuffer;
		public readonly DeviceBuffer lightDataBuffer = _lightDataBuffer;
		public readonly Texture shadowMapArray = _shadowMapArray;
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
			globalConstantBuffer != null && !globalConstantBuffer.IsDisposed &&
			lightDataBuffer != null && !lightDataBuffer.IsDisposed;

		#endregion
	}
}
