using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.Components.ConstantBuffers
{
	[Obsolete($"Replaced by {nameof(CBScene)} and {nameof(CBCamera)}!")]
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Size = byteSize)]
	public struct GlobalConstantBuffer
	{
		#region Fields

		// Camera vectors & matrices:
		public Matrix4x4 mtxWorld2Clip;		// Camera's full projection matrix, transforming from world space to viewport pixel coordinates.
		public Vector4 cameraPosition;      // Camera position, in world space.
		public Vector4 cameraDirection;     // Camera forward facing direction, in world space. (NOTE: This is a float4 because of CB packing rules; data must be packed in 4x4 byte vector blocks)

		// Camera parameters:
		public uint resolutionX;            // Render target width, in pixels.
		public uint resolutionY;            // Render target height, in pixels.
		public float nearClipPlane;			// Camera's near clipping plane distance.
		public float farClipPlane;          // Camera's far clipping plane distance.

		// Lighting:
		public RgbaFloat ambientLightLow;	// Ambient light color and intensity, coming from the bottom (i.e. along Y-axis).
		public RgbaFloat ambientLightMid;	// Ambient light color and intensity, coming equally from sides (i.e. X- and Z-axex).
		public RgbaFloat ambientLightHigh;	// Ambient light color and intensity, coming from the bottom (i.e. against Y-axis).
		public uint lightCount;             // Number of lights in the camera's light source data buffer.
		public float shadowFadeStart;       // Percentage of the shadow distance in projection space where they start fading out.

		#endregion
		#region Constants

		public const int byteSize =
			16 * sizeof(float) +    // camera matrix
			2 * 4 * sizeof(float) + // camera pos+dir
			2 * sizeof(uint) +      // resolution
			2 * sizeof(float) +     // clipping planes
			3 * 4 * sizeof(float) + // ambient light
			1 * sizeof(uint) +      // light count
			1 * sizeof(float);		// shadow fade = 168 bytes

		public const int packedByteSize = 176;

		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDesc = new("CBGlobal", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment);

		#endregion
	}
}
