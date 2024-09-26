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
	public CameraInstance CameraInstance { get; init; } = null!;
	public CommandList CmdList { get; init; } = null!;

	// Camera resources:
	public Framebuffer Framebuffer { get; init; } = null!;
	public ResourceSet ResSetCamera { get; init; } = null!;
	public DeviceBuffer CbCamera { get; init; } = null!;
	public LightDataBuffer LightDataBuffer { get; init; } = null!;
	public ushort CameraResourceVersion { get; init; }

	// Parameters:
	public uint FrameIdx { get; init; }
	public uint PassIdx { get; init; }
	public uint LightCountShadowMapped { get; init; }
	public Matrix4x4 MtxWorld2Clip { get; init; }
	public OutputDescription OutputDesc { get; init; }
	public bool MirrorY { get; init; }
	
	#endregion
}
