#ifndef __HAS_LIGHT__
#define __HAS_LIGHT__

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SOURCES)
//<RES>

struct Light
{
    float3 lightColor;
    float lightIntensity;
    float3 lightPosition;
    uint lightType;
    float3 lightDirection;
    float lightSpotMinDot;
    uint shadowMapIdx;
    float shadowBias;
    uint shadowCascades;
    float shadowCascadeRange;
    float3 shadowDepthBias;
    float _padding;
};

StructuredBuffer<Light> BufLights : register(ps, t0);	// Buffer containing an array of light source data. Number of lights is given in 'CBGlobal.lightCount'.

//</RES>
#endif //FEATURE_LIGHT && FEATURE_LIGHT_SOURCES

#endif //__HAS_LIGHT__
