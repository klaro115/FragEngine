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
		public float spotMinDot;				// Minimum dot product between light ray direction and direction to pixel/fragment for a spot light to apply.
		public Matrix4x4 mtxShadowWorld2Clip;	// Projection matrix, transforming world space position to clip space coordinates.
		public uint shadowMapIdx;               // Index of the shadow map in texture array. Default=0, map at index 0 is always a 'blank' placeholder.
		public float lightMaxRange;				// Maximum distance light rays can travel.

		#endregion
		#region Constants

		public const int byteSize = 3 * 3 * sizeof(float) + 3 * sizeof(float) + 2 * sizeof(uint) + 16 * sizeof(float);   // 120 bytes
		public const int packedByteSize = 128;
		
		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescLightBuffer = new("BufLights", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment);
		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescTexShadowMaps = new("TexShadowMaps", ResourceKind.TextureReadOnly, ShaderStages.Fragment);
		public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescSamplerShadowMaps = new("SamplerShadowMaps", ResourceKind.Sampler, ShaderStages.Fragment);

		#endregion
	}
}
