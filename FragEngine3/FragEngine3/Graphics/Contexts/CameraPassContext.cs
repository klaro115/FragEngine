using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Lighting.Internal;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Contexts;

/// <summary>
/// Context type containing resources and parameters for rendering a camera pass.
/// These values will be passed to all renderers for which draw calls are issued during the camera pass.
/// </summary>
public sealed class CameraPassContext
{
	#region Fields

	// References:
	public required CameraInstance CameraInstance { get; init; }
	public required CommandList CmdList { get; init; }

	// Camera resources:
	public required Framebuffer Framebuffer { get; init; }
	public required ResourceSet ResSetCamera { get; init; }
	public required DeviceBuffer CbCamera { get; init; }
	public required LightDataBuffer LightDataBuffer { get; init; }
	public required ushort CameraResourceVersion { get; init; }

	// Parameters:
	public required uint FrameIdx { get; init; }
	public required uint PassIdx { get; init; }
	public required uint LightCountShadowMapped { get; init; }
	public required Matrix4x4 MtxWorld2Clip { get; init; }
	public required OutputDescription OutputDesc { get; init; }
	public required bool MirrorY { get; init; }
	
	#endregion
}
