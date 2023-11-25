using System.Numerics;
using System.Runtime.InteropServices;

namespace FragEngine3.Graphics.Components.ConstantBuffers
{
	[StructLayout(LayoutKind.Sequential, Pack = 16, Size = byteSize)]
	public struct GlobalConstantBuffer
	{
		#region Fields

		// Camera parameters:
		public uint resolutionX;			// Render target width, in pixels.
		public uint resolutionY;            // Render target height, in pixels.
		public float nearClipPlane;			// Camera's near clipping plane distance.
		public float farClipPlane;          // Camera's far clipping plane distance.

		// Camera vectors & matrices:
		public Vector3 cameraPosition;      // Camera position, in world space.
		public Vector3 cameraDirection;		// Camera forward facing direction, in world space.
		public Matrix4x4 mtxCamera;			// Camera's full projection matrix, transforming from world space to viewport pixel coordinates.

		// Lighting:
		public uint lightCount;				// Number of lights in the camera's light source data buffer.

		#endregion
		#region Constants

		public const int byteSize =
			2 * sizeof(float) +		// clipping planes
			2 * 3 * sizeof(float) +	// camera pos+dir
			16 * sizeof(float) +	// camera matrix
			3 * sizeof(uint);       // res + light count	= 108 bytes

		public const int packedByteSize = 112;
		
		#endregion
	}
}
