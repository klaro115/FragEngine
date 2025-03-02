using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.ConstantBuffers;

[StructLayout(LayoutKind.Explicit, Pack = 4, Size = byteSize)]
[ConstantBufferDataType(ConstantBufferType.CBObject, packedByteSize)]
public struct CBObject
{
    #region Fields

    [FieldOffset(0)]
    public Matrix4x4 mtxLocal2World;    // Object world matrix, transforming vertices from model space to world space.
    [FieldOffset(16 * sizeof(float))]
    public Vector3 worldPosition;       // World space position of the object.
    [FieldOffset(19 * sizeof(float))]
    public float boundingRadius;        // Bounding sphere radius of the object.

    #endregion
    #region Constants

    public const int byteSize =
        16 * sizeof(float) +    // world matrix
        3 * sizeof(float) +     // object pos
        sizeof(float);          // bounding radius		= 80 bytes

    public const int packedByteSize = 80;

    public const string NAME_IN_SHADER = "CBObject";
    public static readonly ResourceLayoutElementDescription resourceLayoutElementDesc = new(NAME_IN_SHADER, ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment);

    #endregion
}
