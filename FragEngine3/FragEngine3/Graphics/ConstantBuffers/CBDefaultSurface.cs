using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.ConstantBuffers;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
[ConstantBufferDataType(ConstantBufferType.CBDefaultSurface, packedByteSize)]
public struct CBDefaultSurface
{
    #region Fields

    public Vector4 tintColor;           // Color tint applied to albedo.
    public float roughness;             // Roughness rating of the surface.
    public float shininess;             // How shiny or metallic the surface is.
    public float reflectionIndex;       // Reflection index of the material's surface.
    public float refractionIndex;       // Refraction index of the material's substance.

    #endregion
    #region Constants

    public const int byteSize = 1 * 4 * sizeof(float) + 4 * sizeof(float);  // 32 bytes
    public const int packedByteSize = 32;                                   // 32 bytes

    public const string NAME_IN_SHADER = "CBDefaultSurface";
    public static readonly ResourceLayoutElementDescription resourceLayoutElementDesc = new(NAME_IN_SHADER, ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment);

    #endregion
}
