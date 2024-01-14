using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.Components.ConstantBuffers
{
	[Obsolete($"Replaced by {nameof(CBObject)}!")]
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
	public struct ObjectDataConstantBuffer
	{
		#region Fields

		public Matrix4x4 mtxLocal2World;
		public Vector3 worldPosition;
		public float boundingRadius;

		#endregion
		#region Constants

		public const int byteSize =
			16 * sizeof(float) +	// Object world matrix
			3 * sizeof(float) +		// world space position
			sizeof(float);			// bounding sphere radius	= 80 bytes

		public const int packedByteSize = 80;

		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDesc = new("CBObject", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment);

		#endregion
	}
}
