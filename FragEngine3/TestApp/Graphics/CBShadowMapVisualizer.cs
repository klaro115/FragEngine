using FragEngine3.Graphics.ConstantBuffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace TestApp.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
[ConstantBufferDataType(ConstantBufferType.Custom, packedByteSize)]
public struct CBShadowMapVisualizer()
{
	#region Fields

	public uint shadowMapIdx = 0;
	public Vector3 padding = Vector3.Zero;

	#endregion
	#region Constants

	public const int byteSize = sizeof(uint) + 3 * sizeof(float);
	public const int packedByteSize = 16;

	#endregion
}
