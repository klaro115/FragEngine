using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.ConstantBuffers;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
[ConstantBufferDataType(ConstantBufferType.CBScene, packedByteSize)]
public struct CBScene
{
	#region Fields

	// Scene lighting:
	public RgbaFloat ambientLightLow;       // Ambient light color and intensity coming from bottom-up.
	public RgbaFloat ambientLightMid;       // Ambient light color and intensity coming from all sides.
	public RgbaFloat ambientLightHigh;      // Ambient light color and intensity coming from top-down.
	public float shadowFadeStart;           // Percentage of the shadow distance in projection space where they start fading out.

	// Time:
	/*
	public float engineRunTime;				// Time since engine started running, in seconds.
	public float engineDeltaTime;           // Time duration of the engine's last update cycle, in seconds.
	public uint engineFrameCount;           // Numver of the update cycles since engine started running.
	public float engineSinTime;				// Sine function of the engine's run time = sin(t)
	*/

	#endregion
	#region Constants

	public const int byteSize =
		3 * 4 * sizeof(float) + // ambient light
		2 * sizeof(uint) +      // light counts
		sizeof(float);          // shadow fade		= 60 bytes

	public const int packedByteSize = 64;

	public const string NAME_IN_SHADER = "CBScene";
	public static readonly ResourceLayoutElementDescription resourceLayoutElementDesc = new(NAME_IN_SHADER, ResourceKind.UniformBuffer, ShaderStages.Fragment);

	#endregion
}
