#ifndef __HAS_SHADOW_MAPS__
#define __HAS_SHADOW_MAPS__

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SOURCES) && defined(FEATURE_LIGHT_SHADOWMAPS)

/****************** INCLUDES: ******************/
//<INC>

#include "../ConstantBuffers/CBCamera.hlsl"
#include "./LightResources.hlsl"

//</INC>
/****************** RESOURCES: *****************/
//<RES>

Texture2DArray<half> TexShadowMaps : register(ps, t1);
StructuredBuffer<float4x4> BufShadowMatrices : register(ps, t3);    // Buffer containing an array of projection matrices for shadow maps, transforming world position to clip space and back.
SamplerState SamplerShadowMaps : register(ps, s0);

//</RES>
/****************** CONSTANTS: *****************/
//<CON>

#define SHADOW_EDGE_FACE_SCALE 10.0

#if defined(FEATURE_LIGHT_SHADOWMAPS_AA) && FEATURE_LIGHT_SHADOWMAPS_AA > 1
    // MSAA offsets for shadow depth sampling:
    #if FEATURE_LIGHT_SHADOWMAPS_AA == 2
        static const float2 shadowSamplingOffsets[] =
        {
            { -0.5 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.5 / FEATURE_LIGHT_SHADOWMAPS_RES,  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES }
        };
    #elif FEATURE_LIGHT_SHADOWMAPS_AA == 4
        static const float2 shadowSamplingOffsets[] =
        {
            {  0, 0 },
            {  0, 0.5 / FEATURE_LIGHT_SHADOWMAPS_RES },
            { -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES },
        };
    #elif FEATURE_LIGHT_SHADOWMAPS_AA == 8
        static const float2 shadowSamplingOffsets[] =
        {
            {  0, 0 },
            {  0,  0.5 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0, -0.5 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.5 / FEATURE_LIGHT_SHADOWMAPS_RES, 0 },
            { -0.5 / FEATURE_LIGHT_SHADOWMAPS_RES, 0 },
            { -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, 0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
            { -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, 0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
        };
    #else
        #error "Shadow sampling count FEATURE_LIGHT_SHADOWMAPS_AA can only be 2, 4 or 8"
    #endif
#endif

//</CON>
/***************** SHADOW MAPS: ****************/
//<FEA>

half CalculateShadowMapLightWeight(const in Light _light, const in float3 _worldPosition, const in float3 _surfaceNormal)
{
    // Determine shadow cascade for this pixel:
    const float cameraDist = length(_worldPosition - cameraPosition.xyz);
    const uint cascadeOffset = (uint)(2 * cameraDist / _light.shadowCascadeRange);
    const uint cascadeIdx = min(cascadeOffset, _light.shadowCascades);
    const uint shadowMapIdx = _light.shadowMapIdx + cascadeIdx;

    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:
    const float4 worldPosBiased = float4(_worldPosition + _surfaceNormal * _light.shadowBias + _light.shadowDepthBias, 1);

    // Transform pixel position to light's clip space, then to UV space:
    float4 shadowProj = mul(BufShadowMatrices[2 * shadowMapIdx], worldPosBiased);
    shadowProj /= shadowProj.w;
    const float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;

#if defined(FEATURE_LIGHT_SHADOWMAPS_AA) && FEATURE_LIGHT_SHADOWMAPS_AA > 1
    // Calculate shadow depth by averaging from multiple samples:
    static const half invShadowSampleCount = 1.0 / FEATURE_LIGHT_SHADOWMAPS_AA;
    half lightWeight = 0;
    for (uint i = 0; i < FEATURE_LIGHT_SHADOWMAPS_AA; ++i)
    {
        const float2 shadowSampleUv = shadowUv + shadowSamplingOffsets[i];
        const half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowSampleUv.x, shadowSampleUv.y, shadowMapIdx));
        lightWeight += shadowDepth > shadowProj.z ? 1 : 0;
    }
    lightWeight *= invShadowSampleCount;
#else
    // Calculate shadow depth from a single sample:
    const half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, shadowMapIdx));
    half lightWeight = shadowDepth > shadowProj.z ? 1 : 0;
#endif //FEATURE_LIGHT_SHADOWMAPS_AA
    
    // Fade shadows out near boundaries of UV/Depth space:
    if (_light.lightType == 2 && shadowMapIdx == _light.shadowCascades)
    {
        const half3 edgeUv = half3(shadowUv, shadowProj.z) * SHADOW_EDGE_FACE_SCALE;
        const half3 edgeMax = min(min(edgeUv, SHADOW_EDGE_FACE_SCALE - edgeUv), 1);
        const half k = 1 - min(min(edgeMax.x, edgeMax.y), edgeMax.z);
        lightWeight = lerp(lightWeight, 1.0, clamp(k, 0, 1));
    }

    return lightWeight;
}

//</FEA>
#endif

/***************** FUNCTIONS: ******************/
//<FNC>

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SOURCES)

half ApplyShadowMapLightWeight(const in Light _light, const in float3 _worldPosition, const in float3 _surfaceNormal)
{
#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SHADOWMAPS)
	return CalculateShadowMapLightWeight(_light, _worldPosition, _surfaceNormal);
#else
	return 1.0;
#endif
}

#endif //FEATURE_LIGHT && FEATURE_LIGHT_SOURCES

//</FNC>
#endif //__HAS_SHADOW_MAPS__
