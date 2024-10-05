#ifndef __HAS_PARALLAX__
#define __HAS_PARALLAX__

#ifdef FEATURE_PARALLAX

/****************** INCLUDES: ******************/
//<INC>

#include "./ConstantBuffers/CBCamera.hlsl"

//<INC>
/****************** RESOURCES: *****************/
//<RES>

Texture2D<half> TexParallax : register(ps, t6);

#ifndef HAS_SAMPLER_MAIN
#define HAS_SAMPLER_MAIN
    SamplerState SamplerMain : register(s1);
#endif

//</RES>
/****************** PARALLAX: ******************/
//<FEA>

float3 ProjectOnPlane(const float3 _vector, const float3 _planeNormal)
{
    return _vector - dot(_vector, _planeNormal);
}

half2 WorldOffset2Pixel(
    const float3 _worldOffset,
    const in float3 _worldPosition,
    const in half2 _uv)
{
    const float3 ddxPos = ddx(_worldPosition);
    const float3 ddyPos = ddy(_worldPosition);
    const float invWorldPerPixelX = 1.0 / length(ddxPos);
    const float invWorldPerPixelY = 1.0 / length(ddyPos);
    return
        ddx(_uv) * (half)dot(_worldOffset, ddxPos * invWorldPerPixelX) +
        ddy(_uv) * (half)dot(_worldOffset, ddyPos * invWorldPerPixelY);
}

half2 CalculateParallaxMap(const in float3 _worldPosition, const in float3 _surfaceNormal, const half2 _uv)
{
    static const float MAX_DEPTH = 0.05;

    const float3 viewOffset = _worldPosition - cameraPosition.xyz;
    
#ifdef FEATURE_PARALLAX_FULL
    static const uint MAX_ITERATIONS = 6;

    const float invViewDist = 1.0 / length(viewOffset);
    const float3 viewDir = viewOffset * invViewDist;

    const float3 maxRayOffset = viewDir * abs(MAX_DEPTH / dot(viewDir, _surfaceNormal));
    const float3 maxSurfaceOffset = ProjectOnPlane(maxRayOffset, _surfaceNormal);
    const half2 maxUvOffset = WorldOffset2Pixel(maxSurfaceOffset, _worldPosition, _uv) * 200 * invViewDist;

    half2 uvOffset;
    half2 curUV = _uv;
    float minK = 0.0;
    float maxK = 1.0;

    for (uint i = 0; i < MAX_ITERATIONS; ++i)
    {
        float k = 0.5 * (minK + maxK);
        
        const float3 rayOffset = k * maxRayOffset;
        uvOffset = k * maxUvOffset;
        curUV = _uv + uvOffset;

        const half sampledHeight = (1.0 - TexParallax.Sample(SamplerMain, curUV)) * MAX_DEPTH;
        const half rayHeight = abs(dot(rayOffset, _surfaceNormal));

        if (sampledHeight > rayHeight)
        {
            minK = k;
        }
        else
        {
            maxK = k;
        }
    }
    return curUV + normalize(uvOffset) * 0.002;

#else
    const float3 surfaceDir = normalize(ProjectOnPlane(viewOffset, _surfaceNormal));
    
    const half depth = TexParallax.Sample(SamplerMain, _uv) * MAX_DEPTH;

    return _uv - WorldOffset2Pixel(surfaceDir * depth, _worldPosition, _uv) * 100;
#endif //FEATURE_PARALLAX_FULL
}

//</FEA>
#endif //FEATURE_PARALLAX

/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyParallaxMap(inout float2 _uv, const in float3 _worldPosition, const in float3 _inputNormal)
{
#ifdef FEATURE_PARALLAX
    // Recalculate UV from parallax map:
    _uv = CalculateParallaxMap(_worldPosition, _inputNormal, _uv);
#endif //FEATURE_PARALLAX
}

//</FNC>
#endif //__HAS_PARALLAX__
