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
	public float shaderTime;                // Timer value for time-based shader effects, in seconds. This mirrors to the time manager's ShaderTime.
	public float deltaTime;                 // Time duration of the last main loop cycle, in seconds.
	public float sinTime;					// Sine function of the engine's run time = sin(t)
	public uint frameCounter;               // Number of main loop cycles since application startup.
	*/

	#endregion
	#region Constants

	public const int byteSize =
		3 * 4 * sizeof(float) + // ambient light
		2 * sizeof(uint) +      // light counts
		sizeof(float);          // shadow fade		= 60 bytes
		/*
		3 * sizeof(float) +     // time values
		sizeof(uint);           // frame count		= 76 bytes
		*/

	public const int packedByteSize = 64;
	//public const int packedByteSize = 80;

	public const string NAME_IN_SHADER = "CBScene";
	public static readonly ResourceLayoutElementDescription resourceLayoutElementDesc = new(NAME_IN_SHADER, ResourceKind.UniformBuffer, ShaderStages.Fragment);

	#endregion
}
