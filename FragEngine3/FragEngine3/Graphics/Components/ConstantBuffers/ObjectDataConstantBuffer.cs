using System.Numerics;
using System.Runtime.InteropServices;

namespace FragEngine3.Graphics.Components.ConstantBuffers
{
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 16, Size = byteSize)]
	public struct ObjectDataConstantBuffer
	{
		#region Fields

		public Matrix4x4 mtxInvWorld;
		public Vector3 worldPosition;
		public float boundingRadius;

		#endregion
		#region Constants

		public const int byteSize =
			16 * sizeof(float) +	// Inverse world matrix
			3 * sizeof(float) +		// world space position
			sizeof(float);			// bounding sphere radius	= 80 bytes

		public const int packedByteSize = 80;

		#endregion
	}
}
