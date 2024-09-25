using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.ConstantBuffers;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
public struct CBCamera
{
    #region Fields

    // Camera vectors & matrices:
    public Matrix4x4 mtxWorld2Clip;         // Camera's full projection matrix, transforming from world space to clip space coordinates.
    public Vector4 cameraPosition;          // Camera position, in world space.
    public Vector4 cameraDirection;         // Camera forward facing direction, in world space.
    public Matrix4x4 mtxInvCameraMotion;    // Camera movement matrix, encoding inverse motion/transformation from current to previous frame.

    // Camera parameters:
    public uint cameraIdx;                  // Index of the currently drawing camera.
    public uint resolutionX;                // Render target width, in pixels.
    public uint resolutionY;                // Render target height, in pixels.
    public float nearClipPlane;             // Camera's near clipping plane distance.
    public float farClipPlane;              // Camera's far clipping plane distance.

    // Per-camera lighting:
    public uint lightCount;                 // Total number of lights.
    public uint shadowMappedLightCount;     // Total number of lights that have a layer of the shadow map texture array assigned.

    #endregion
    #region Constants

    public const int byteSize =
        16 * sizeof(float) +    // projection matrix
        16 * sizeof(float) +    // motion matrix
        2 * 4 * sizeof(float) + // camera vectors
        3 * sizeof(uint) +      // camera idx & res
        2 * sizeof(float) +     // clip planes
        2 * sizeof(uint);       // light counts			= 184 bytes

    public const int packedByteSize = 192;

    public const string NAME_IN_SHADER = "CBCamera";
    public static readonly ResourceLayoutElementDescription resourceLayoutElementDesc = new(NAME_IN_SHADER, ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment);

    #endregion
}
