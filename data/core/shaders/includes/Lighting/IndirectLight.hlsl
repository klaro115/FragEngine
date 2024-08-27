#ifndef __HAS_INDIRECT_LIGHT__
#define __HAS_INDIRECT_LIGHT__

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SOURCES) && defined(FEATURE_LIGHT_SHADOWMAPS) && defined(FEATURE_LIGHT_INDIRECT) && FEATURE_LIGHT_INDIRECT > 1

/****************** INCLUDES: ******************/
//<INC>

#include "./ShadowMaps.hlsl"

//</INC>
/****************** RESOURCES: *****************/
//<RES>

#ifndef HAS_SHADOW_NORMAL_MAPS
#define HAS_SHADOW_NORMAL_MAPS
    Texture2DArray<half3> TexShadowNormalMaps : register(ps, t2);
#endif

//</RES>
/*************** INDIRECT LIGHT: ***************/
//<FEA>

half3 CalculateIndirectLightScatter(const in Light _light, const in float3 _worldPosition, const in float3 _surfaceNormal)
{
    static const int halfKernel = FEATURE_LIGHT_INDIRECT / 2;
    static const half uvKernelSteps = 1.0 / 256;
    static const float bounceAmount = 0.025;

    // Determine shadow cascade for this pixel:
    const float cameraDist = length(_worldPosition - cameraPosition.xyz);
    const uint cascadeOffset = (uint)(2 * cameraDist / _light.shadowCascadeRange);
    const uint cascadeIdx = min(cascadeOffset, _light.shadowCascades);
    const uint shadowMapIdx = _light.shadowMapIdx + cascadeIdx;

    const float4x4 mtxShadowWorld2Clip = BufShadowMatrices[2 * shadowMapIdx];
    const float4x4 mtxShadowClip2World = BufShadowMatrices[2 * shadowMapIdx + 1];

    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:
    const float4 worldPosBiased = float4(_worldPosition + _surfaceNormal * _light.shadowBias, 1);

    // Transform pixel position to light's clip space, then to UV space:
    float4 shadowProj = mul(mtxShadowWorld2Clip, worldPosBiased);
    shadowProj /= shadowProj.w;
    const float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;

    float lightBounceSum = 0.0;

    for (int y = -halfKernel; y < halfKernel; ++y)
    {
        const half uvY = shadowUv.y + y * uvKernelSteps;
        for (int x = -halfKernel; x < halfKernel; ++x)
        {
            const half uvX = shadowUv.x + x * uvKernelSteps;
            const half3 uv = half3(uvX, uvY, _light.shadowMapIdx);

            const half3 normal = TexShadowNormalMaps.Sample(SamplerShadowMaps, uv) * 2 - 1;
            const half depth = TexShadowMaps.Sample(SamplerShadowMaps, uv);

            const half4 posClipSpace = half4(uvX * 2 - 1, 1 - uvY * 2, depth, 1);
            const float3 posWorld = mul(mtxShadowClip2World, posClipSpace).xyz;

            // Determine approximate lighting at sampled point, pre-bounce:
            const float3 lightOffset = posWorld - _light.lightPosition;
            const float intensityPreBounce = max(-dot(lightOffset / length(lightOffset), normal), 0) / dot(lightOffset, lightOffset);

            // Determine radiated lighting at center point, post-bounce:
            const float3 offsetBounced = worldPosBiased.xyz - posWorld;
            const float distSqBounced = dot(offsetBounced, offsetBounced);
            const float intensityPostBounce = intensityPreBounce / distSqBounced;

            lightBounceSum += dot(offsetBounced, _surfaceNormal) < 0 ? intensityPostBounce : 0; //TODO: _surfaceNormal is wrong! That's geometry normal, but here we need surface/shaded normal!
        }
    }
    lightBounceSum *= bounceAmount;

    return lightBounceSum * _light.lightIntensity * _light.lightColor;
}

//</FEA>
#endif //FEATURE_LIGHT_INDIRECT

/***************** FUNCTIONS: ******************/
//<FNC>

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SOURCES) && defined(FEATURE_LIGHT_SHADOWMAPS)

void ApplyIndirectLighting(inout half3 _lightIntensity, const in Light _light, const in float3 _worldPosition, const in float3 _surfaceNormal)
{
#if defined(FEATURE_LIGHT_INDIRECT) && FEATURE_LIGHT_INDIRECT > 1
	_lightIntensity += CalculateIndirectLightScatter(_light, _worldPosition, _surfaceNormal);
#endif //FEATURE_LIGHT_INDIRECT
}

#endif //FEATURE_LIGHT && FEATURE_LIGHT_SOURCES

//</FNC>
#endif //__HAS_INDIRECT_LIGHT__
