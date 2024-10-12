#ifndef __HAS_LIGHTING__
#define __HAS_LIGHTING__

#ifdef FEATURE_LIGHT

/****************** INCLUDES: ******************/
//<INC>

#include "./includes/Lighting/AmbientLight.hlsl"
#include "./includes/Lighting/Lightmap.hlsl"
#include "./includes/Lighting/ShadowMaps.hlsl"
#include "./includes/Lighting/IndirectLight.hlsl"

//</INC>
/******************* MODELS: *******************/
//<FEA>

#ifdef FEATURE_LIGHT_SOURCES

half3 CalculatePhongLighting(const in Light _light, const in float3 _worldPosition, const in float3 _worldNormal)
{
    half3 lightIntens = (half3)(_light.lightColor * _light.lightIntensity);
    float3 lightRayDir;

    // Directional light:
    if (_light.lightType == 2)
    {
        lightRayDir = _light.lightDirection;
    }
    // Point or Spot light:
    else
    {
        const float3 lightOffset = _worldPosition - _light.lightPosition;
        lightIntens /= (half)dot(lightOffset, lightOffset);
        lightRayDir = normalize(lightOffset);

        // Spot light angle:
        if (_light.lightType == 1 && dot(_light.lightDirection, lightRayDir) < _light.lightSpotMinDot)
        {
            lightIntens = half3(0, 0, 0);
        }
    }

    const half lightDot = max(-(half)dot(lightRayDir, _worldNormal), 0.0);
    return lightIntens.xyz * lightDot;
}

//... (insert further lighting models here)

#endif //FEATURE_LIGHT_SOURCES

/****************** LIGHTING: ******************/

half3 CalculateDirectLightIntensity(const in Light _light, const in float3 _worldPosition, const in float3 _worldNormal)
{
#if defined(FEATURE_LIGHT_MODEL) && FEATURE_LIGHT_MODEL == Phong
	return CalculatePhongLighting(_light, _worldPosition, _worldNormal);
#else
	return half3(0, 0, 0);
#endif //FEATURE_LIGHT_MODEL
}

half3 CalculateTotalLightIntensity(const in float3 _worldPosition, const in float3 _worldNormal, const in float3 _surfaceNormal, const in float2 _uv)
{
	half3 totalLightIntensity = half3(0, 0, 0);
	
	ApplyAmbientLightIntensity(totalLightIntensity, _worldNormal);
	ApplyLightmaps(totalLightIntensity, _uv);

    #ifdef FEATURE_LIGHT_SOURCES
    {
        uint i = 0;
        #ifdef FEATURE_LIGHT_SHADOWMAPS
        // Shadow-casting light sources:
        for (; i < shadowMappedLightCount; ++i)
        {
            Light light = BufLights[i];

            const half3 lightIntensity = CalculateDirectLightIntensity(light, _worldPosition, _worldNormal);
            const half lightWeight = ApplyShadowMapLightWeight(light, _worldPosition, _surfaceNormal);
            totalLightIntensity += lightIntensity * lightWeight;

			ApplyIndirectLighting(totalLightIntensity, light, _worldPosition, _surfaceNormal);
        }
        #else
        uint shadowMappedLightCount = 0;
        #endif //FEATURE_LIGHT_SHADOWMAPS
        // Simple light sources:
        for (i = shadowMappedLightCount; i < lightCount; ++i)
        {
            totalLightIntensity += CalculateDirectLightIntensity(BufLights[i], _worldPosition, _worldNormal);
        }
    }
    #endif //FEATURE_LIGHT_SOURCES

    return totalLightIntensity;
}

//</FEA>
#endif //FEATURE_LIGHT

void ApplyLighting(inout half4 _albedo, const in float3 _worldPosition, const in float3 _worldNormal, const in float3 _surfaceNormal, const in float2 _uv)
{
#ifdef FEATURE_LIGHT
    const half3 totalLightIntensity = CalculateTotalLightIntensity(_worldPosition, _worldNormal, _surfaceNormal, _uv);
    _albedo *= half4(totalLightIntensity, 1);
#endif
}

#endif //__HAS_LIGHTING__
