using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.Lighting
{
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
        public float shadowBias;                // Bias distance along surface normal when comparing against shadow depth. (this reduces stair-stepping artifacts)
        public uint shadowCascades;             // Number of shadow cascades used by this light source. Default=0, maps at indices after current will contain cascades.
        public float shadowCascadeRange;        // Radius within which the first cascade is valid. Cascade index is increased by each whole multiple of this distance.

        #endregion
        #region Constants

        public const int byteSize = 3 * 3 * sizeof(float) + 4 * sizeof(float) + 3 * sizeof(uint);   // 64 bytes
        public const int packedByteSize = 64;

        public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescBufLights = new("BufLights", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment);
        public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescTexShadowMaps = new("TexShadowMaps", ResourceKind.TextureReadOnly, ShaderStages.Fragment);
        public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescBufShadowMatrices = new("BufShadowMatrices", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment);
        public static readonly ResourceLayoutElementDescription ResourceLayoutElementDescSamplerShadowMaps = new("SamplerShadowMaps", ResourceKind.Sampler, ShaderStages.Fragment);

        #endregion
    }
}
