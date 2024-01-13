using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.Components.ConstantBuffers
{
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
	public struct LightSourceData
	{
		#region Fields

		public Vector3 color;					// Color of the light emitted by this light source.
		public float intensity;					// Intensity or strength the light emitted by this light source.
		public Vector3 position;				// World space position of the light source. Required for point and spot lights.
		public uint type;						// ID of the light source type. 0=Point, 1=Spot, 2=Directional.
		public Vector3 direction;				// Direction vector in which the light source is pointing. Required for spot and directional lights.
		public float spotAngleAcos;				// Arc-cosine of the light angle of a spot light.
		public Matrix4x4 mtxShadowWorld2Uv;		// Projection matrix, transforming world space position to UV coordinates on a shadow map.
		public uint shadowMapIdx;				// Index of the shadow map in texture array. Default=0, map at index 0 is always a 'blank' placeholder.

		#endregion
		#region Constants

		public const int byteSize = 3 * 3 * sizeof(float) + 2 * sizeof(float) + 2 * sizeof(uint) + 16 * sizeof(float);   // 116 bytes
		public const int packedByteSize = 128;
		
		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescLightBuffer = new("BufLights", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment);
		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescShadowMaps = new("TexShadowMaps", ResourceKind.TextureReadOnly, ShaderStages.Fragment);

		#endregion
	}
}
