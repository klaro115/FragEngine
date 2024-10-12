#ifndef __HAS_HEIGHTMAPS__
#define __HAS_HEIGHTMAPS__

/******************* INCLUDES: *****************/
//<INC>

#include "../VertexData/VertexInput.hlsl"

//</INC>
#ifdef FEATURE_HEIGHTMAP
/****************** RESOURCES: *****************/
//<RES>

Texture2D<float> TexHeightmap : register(vs, t6);
SamplerState SamplerHeightmap : register(vs, s2);

// Constant buffer containing heightmap settings:
cbuffer CBHeightmap : register(b4)
{
    float4 heightmapTiling;         // Offset (XY) and scale (ZW) of the heightmap. This can be used to crop a section of the height map.
    float minAltitude;              // Minimum displacement along surface normal, all vertices are moved by at least this much.
    float maxAltitude;              // Maximum displacement along surface normal, no vertex will move further than this.
    float altitudeRange;            // Difference between minimum and maximum displacement altitudes.
};

//</RES>
/***************** HEIGHTMAP: ******************/
//<FEA>

float GetHeightmapDisplacement(const in float2 _inputUv)
{
    const float2 heightmapUv = (_inputUv + heightmapTiling.xy) * heightmapTiling.zw;
    const float heightFactor = TexHeightmap.SampleLevel(SamplerHeightmap, heightmapUv, 0);
    return minAltitude + heightFactor * altitudeRange;
}

//</FEA>
#endif //FEATURE_HEIGHTMAP
/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyHeightmap(inout VertexInput_Basic _inputBasic)
{
#ifdef FEATURE_HEIGHTMAP
    const float displacement = GetHeightmapDisplacement(_inputBasic.uv);

    _inputBasic.position += _inputBasic.normal * displacement;
#endif //FEATURE_HEIGHTMAP
}

//</FNC>
#endif //__HAS_HEIGHTMAPS__
