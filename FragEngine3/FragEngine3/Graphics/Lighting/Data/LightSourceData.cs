﻿using System.Numerics;
using System.Runtime.InteropServices;

namespace FragEngine3.Graphics.Lighting.Data
{
	/// <summary>
	/// Structure containing packed data for one light source.<para/>
	/// NOTE: This is the GPU-side data type, for uploading to a device buffer. For serialization of light components, see '<see cref="Components.Data.LightData"/>' instead.
	/// </summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
	public struct LightSourceData
	{
		#region Fields

		public Vector3 color;                   // Color of the light emitted by this light source.
		public float intensity;                 // Intensity or strength the light emitted by this light source.
		public Vector3 position;                // World space position of the light source. Required for point and spot lights.
		public uint type;                       // ID of the light source type. 0=Point, 1=Spot, 2=Directional.
		public Vector3 direction;               // Direction vector in which the light source is pointing. Required for spot and directional lights.
		public float spotMinDot;                // Minimum dot product between light ray direction and direction to pixel/fragment for a spot light to apply.
		public uint shadowMapIdx;               // Index of the shadow map in texture array. Default=0, map at index 0 is always a 'blank' placeholder.
		public float shadowNormalBias;          // Bias distance along surface normal when comparing against shadow depth. (this reduces stair-stepping artifacts)
		public uint shadowCascades;             // Number of shadow cascades used by this light source. Default=0, maps at indices after current will contain cascades.
		public float shadowCascadeRange;        // Radius within which the first cascade is valid. Cascade index is increased by each whole multiple of this distance.
		public Vector3 shadowDepthBias;         // Bias offset towards light source when comparing against shadow depth. (this reduces stair-stepping artifacts)
		public float padding;                   // [reserved]

		#endregion
		#region Constants

		public const int byteSize = 4 * 3 * sizeof(float) + 5 * sizeof(float) + 3 * sizeof(uint);   // 80 bytes
		public const int packedByteSize = 80;

		#endregion
	}
}
