#ifndef __HAS_AMBIENT_LIGHT__
#define __HAS_AMBIENT_LIGHT__

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_AMBIENT)

/****************** INCLUDES: ******************/
//<INC>

#include "../ConstantBuffers/CBScene.hlsl"

//</INC>
/****************** AMBIENT: *******************/
//<FEA>

half3 CalculateAmbientLight(const in float3 _worldNormal)
{
	// Evaluate a 2-stage gradient based on vertical orientation of the surface:
    const half dotY = (half)dot(_worldNormal, float3(0, 1, 0));
    const half wLow = max(-dotY, 0);        // Bottom-up weight
    const half wHigh = max(dotY, 0);        // Top-down weight
    const half wMid = 1.0 - wHigh - wLow;   // Horizontal plane weight
    return (wLow * (half4)ambientLightLow + wHigh * (half4)ambientLightHigh + wMid * (half4)ambientLightMid).xyz;
}

//</FEA>
#endif

/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyAmbientLightIntensity(inout half3 _lightIntensity, const in float3 _worldNormal)
{
#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_AMBIENT)
	_lightIntensity += CalculateAmbientLight(_worldNormal);
#endif //FEATURE_LIGHT && FEATURE_LIGHT_AMBIENT
}

//</FEA>
#endif //__HAS_AMBIENT_LIGHT__
