using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.ConstantBuffers;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
public struct CBScene
{
    #region Fields

    // Scene lighting:
    public RgbaFloat ambientLightLow;       // Ambient light color and intensity coming from bottom-up.
    public RgbaFloat ambientLightMid;       // Ambient light color and intensity coming from all sides.
    public RgbaFloat ambientLightHigh;      // Ambient light color and intensity coming from top-down.
    public float shadowFadeStart;           // Percentage of the shadow distance in projection space where they start fading out.

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
