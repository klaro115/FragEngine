#ifndef __HAS_LIGHTMAP__
#define __HAS_LIGHTMAP__

#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_LIGHTMAP)

/****************** RESOURCES: *****************/
//<RES>

Texture2D<half3> TexLightmap : register(ps, t7);

#ifndef HAS_SAMPLER_MAIN
#define HAS_SAMPLER_MAIN
    SamplerState SamplerMain : register(s1);
#endif

//</RES>
/****************** LIGHTMAP: ******************/
//<FEA>

half3 CalculateLightmaps(const in float2 _uv)
{
    return TexLightmap.Sample(SamplerMain, _uv);
}

//</FEA>
#endif

/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyLightmaps(inout half3 _lightIntensity, const in float2 _uv)
{
#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_LIGHTMAP)
	_lightIntensity += CalculateLightmaps(_uv);
#endif
}

//</FNC>
#endif //__HAS_LIGHTMAP__
