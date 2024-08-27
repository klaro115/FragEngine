#ifndef __HAS_NORMALS__
#define __HAS_NORMALS__

#ifdef FEATURE_NORMALS

/****************** RESOURCES: *****************/
//<RES>

Texture2D<half3> TexNormal : register(ps, t5);

#ifndef HAS_SAMPLER_MAIN
#define HAS_SAMPLER_MAIN
    SamplerState SamplerMain : register(s1);
#endif

//TODO: Add support for custom normal sampler, if feature flag is set.

//</RES>
/******************* NORMALS: ******************/
//<FEA>

half3 UnpackNormalMap(const in half3 _texNormal)
{
    // Unpack direction vector from normal map colors:
    return half3(_texNormal.x * 2 - 1, _texNormal.z, _texNormal.y * 2 - 1); // NOTE: Texture normals are expected to be in OpenGL standard.
}

half3 CalculateNormalMap(const in half3 _worldNormal, const in half3 _worldTangent, const in half3 _worldBinormal, in half3 _texNormal)
{
    _texNormal = UnpackNormalMap(_texNormal);

    // Create rotation matrix, projecting from flat surface (UV) space to surface in world space:
    const half3x3 mtxNormalRot =
    {
        _worldBinormal.x, _worldNormal.x, _worldTangent.x,
        _worldBinormal.y, _worldNormal.y, _worldTangent.y,
        _worldBinormal.z, _worldNormal.z, _worldTangent.z,
    };
    const half3 normal = mul(mtxNormalRot, _texNormal);
    return normal;
}

//</FEA>
#endif //FEATURE_NORMALS

/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyNormalMap(inout float3 _surfaceNormal, const in half3 _worldTangent, const in half3 _worldBinormal, const in float2 _uv)
{
#ifdef FEATURE_NORMALS
    // Calculate normals from normal map:
    const half3 texNormal = TexNormal.Sample(SamplerMain, _uv);
    _surfaceNormal = CalculateNormalMap(_surfaceNormal, _worldTangent, _worldBinormal, texNormal);
#endif //FEATURE_NORMALS
}

//</FNC>
#endif //__HAS_NORMALS__
