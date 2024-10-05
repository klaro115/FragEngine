#ifndef __HAS_ALBEDO__
#define __HAS_ALBEDO__

/****************** INCLUDES: ******************/
//<INC>

#if defined(FEATURE_ALBEDO_TEXTURE) && FEATURE_ALBEDO_TEXTURE == 1 && defined(FEATURE_ALBEDO_UNSAMPLED)
#include "./ConstantBuffers/CBCamera.hlsl"
#endif

//</INC>
/****************** RESOURCES: *****************/
//<RES>

#if defined(FEATURE_ALBEDO_TEXTURE) && FEATURE_ALBEDO_TEXTURE == 1
Texture2D<half4> TexMain : register(ps, t4);
#endif //FEATURE_ALBEDO_TEXTURE == 1

#if !defined(HAS_SAMPLER_MAIN) && defined(FEATURE_ALBEDO_TEXTURE) && FEATURE_ALBEDO_TEXTURE == 1 && defined(FEATURE_ALBEDO_UNSAMPLED)
#define HAS_SAMPLER_MAIN
    SamplerState SamplerMain : register(s1);
#endif //HAS_SAMPLER_MAIN

//</RES>
/***************** FUNCTIONS: ******************/
//<FNC>

half4 GetAlbedoColor(const in float2 _uv)
{
#if FEATURE_ALBEDO_TEXTURE == 1
	#ifdef FEATURE_ALBEDO_UNSAMPLED
		int3 posPixel = int3(_uv * float2(resolutionX, resolutionY), 0);
		return TexMain.Load(posPixel);
	#else
	    return TexMain.Sample(SamplerMain, uv);
	#endif //HAS_SAMPLER_MAIN
#elif defined(FEATURE_ALBEDO_COLOR)
	return FEATURE_ALBEDO_COLOR;
#else
	return half4(1, 1, 1, 1);
#endif //FEATURE_ALBEDO_TEXTURE
}

//</FNC>

#endif //__HAS_ALBEDO__
