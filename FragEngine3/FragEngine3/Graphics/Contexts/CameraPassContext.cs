using FragEngine3.Graphics.Cameras;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class CameraPassContext(
		// References:
		CameraInstance _cameraInstance,
		CommandList _cmdList,

		// Camera resources:
		Framebuffer _framebuffer,
		ResourceSet _resSetCamera,
		DeviceBuffer _cbCamera,
		DeviceBuffer _bufLights,

		// Parameters:
		uint _frameIdx,
		uint _passIdx,
		uint _lightCount,
		uint _lightCountShadowMapped,
		in Matrix4x4 _mtxWorld2Clip)
	{
		#region Fields

		// References:
		public readonly CameraInstance cameraInstance = _cameraInstance;
		public readonly CommandList cmdList = _cmdList;

		// Camera resources:
		public readonly Framebuffer framebuffer = _framebuffer;
		public readonly ResourceSet resSetCamera = _resSetCamera;
		public readonly DeviceBuffer cbCamera = _cbCamera;
		public readonly DeviceBuffer bufLights = _bufLights;

		// Parameters:
		public readonly uint frameIdx = _frameIdx;
		public readonly uint passIdx = _passIdx;
		public readonly uint lightCount = _lightCount;
		public readonly uint lightCountShadowMapped = Math.Min(_lightCountShadowMapped, _lightCount);
		public readonly Matrix4x4 mtxWorld2Clip = _mtxWorld2Clip;
		public readonly OutputDescription outputDesc = _framebuffer.OutputDescription;
		public readonly bool mirrorY = _cameraInstance.ProjectionSettings.mirrorY;

		#endregion
	}
}
