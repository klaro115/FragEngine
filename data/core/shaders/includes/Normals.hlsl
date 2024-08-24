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

half3 ApplyNormalMap(const in half3 _worldNormal, const in half3 _worldTangent, const in half3 _worldBinormal, in half3 _texNormal)
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

half3 CalculateSurfaceNormal(const in float3 _inputNormal)
{
#ifdef FEATURE_NORMALS
    // Calculate normals from normal map:
    const half3 normal = TexNormal.Sample(SamplerMain, uv);
    return ApplyNormalMap(_inputNormal, half3(0, 0, 1), half3(1, 0, 0), normal);
#else
	// Use surface normal from input as-is:
    return _inputNormal;
#endif //FEATURE_NORMALS
}

//</FNC>
#endif //__HAS_NORMALS__
