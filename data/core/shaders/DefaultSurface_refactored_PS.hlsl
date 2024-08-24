//#pragma pack_matrix( column_major )

/******************* DEFINES: ******************/

// Albedo:
#define FEATURE_ALBEDO_TEXTURE 1                // Whether to initialize albedo color from main texture. If false, a color literal is used
#define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)  // Color literal from which albedo may be initialized

// Normals:
#define FEATURE_NORMALS                         // Whether to use normal maps in all further shading
#define FEATURE_PARALLAX                        // Whether to use height/parallax maps to modulate UV sampling
#define FEATURE_PARALLAX_FULL                   // Whether to use full iteratively traced parallax with occlusion, instead of just simple UV offsetting.

// Lighting:
#define FEATURE_LIGHT                           // Whether to apply lighting
#define FEATURE_LIGHT_AMBIENT                   // Whether to add directional ambient intensity to base lighting
#define FEATURE_LIGHT_LIGHTMAPS                 // Whether to add light map intensity to base lighting
#define FEATURE_LIGHT_SOURCES                   // Whether to use light sources from the scene to light up the object
#define FEATURE_LIGHT_MODEL Phong
#define FEATURE_LIGHT_SHADOWMAPS                // Whether to use shadow maps to mask out light rays coming from light sources
#define FEATURE_LIGHT_SHADOWMAPS_RES 1024       // The resolution of shadow maps, in pixels per side.
#define FEATURE_LIGHT_SHADOWMAPS_AA 2           // If defined, controls the number of depth samples read from shadow map per pixel 
#define FEATURE_LIGHT_INDIRECT 5                // If defined, controls the number of indirect light samples per pixel, sample count is NxN, must be 2 or higher

// Variants:
#define VARIANT_EXTENDED                        // Whether to always create a shader variant using extended surface data
#define VARIANT_BLENDSHAPES                     // Whether to always create a shader variant using blend shape data
#define VARIANT_ANIMATED                        // Whether to always create a shader variant using bone animation data

#if FEATURE_ALBEDO_TEXTURE == 0
    #ifndef FEATURE_ALBEDO_COLOR
        #define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)
    #endif
#endif

//^TODO: Instead of replacing on import, prepend these only before compilation?

/****************** INCLUDES: ******************/
//<INC>

#include "./includes/Normals.hlsl"
#include "./includes/Parallax.hlsl"
#include "./includes/Lighting/AmbientLight.hlsl"
#include "./includes/Lighting/Lightmap.hlsl"

//<INC>
/****************** CONSTANTS: *****************/

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
};

// Constant buffer containing material-specific settings:
cbuffer CBDefaultSurface : register(b3)
{
    float4 tintColor;               // Color tint applied to albedo.
    float roughness;                // Roughness rating of the surface.
    float shininess;                // How shiny or metallic the surface is.
    float reflectionIndex;          // Reflection index of the material's surface.
    float refractionIndex;          // Refraction index of the material's substance.
};

/****************** RESOURCES: *****************/

// ResSetCamera:

#ifdef FEATURE_LIGHT_SOURCES
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

StructuredBuffer<Light> BufLights : register(ps, t0);               // Buffer containing an array of light source data. Number of lights is given in 'CBGlobal.lightCount'.
#endif

#ifdef FEATURE_LIGHT_SHADOWMAPS
Texture2DArray<half> TexShadowMaps : register(ps, t1);
StructuredBuffer<float4x4> BufShadowMatrices : register(ps, t3);    // Buffer containing an array of projection matrices for shadow maps, transforming world position to clip space and back.
SamplerState SamplerShadowMaps : register(ps, s0);
    #if defined(FEATURE_LIGHT_INDIRECT) && FEATURE_LIGHT_INDIRECT > 1
    Texture2DArray<half3> TexShadowNormalMaps : register(ps, t2);
    #endif
#endif

// ResSetBound:

#if FEATURE_ALBEDO_TEXTURE == 1
Texture2D<half4> TexMain : register(ps, t4);
#endif //FEATURE_ALBEDO_TEXTURE == 1

#if !defined(HAS_SAMPLER_MAIN) && FEATURE_ALBEDO_TEXTURE == 1
#define HAS_SAMPLER_MAIN
    SamplerState SamplerMain : register(s1);
#endif //HAS_SAMPLER_MAIN

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

#ifdef VARIANT_EXTENDED
struct VertexOutput_Extended
{
    float3 tangent : TANGENT0;
    float3 binormal : NORMAL1;
    float2 uv2 : TEXCOORD1;
};
#endif //VARIANT_EXTENDED

#ifdef FEATURE_LIGHT

/****************** LIGHTING: ******************/

#ifdef FEATURE_LIGHT_SOURCES
#if FEATURE_LIGHT_MODEL == Phong
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
#endif //FEATURE_LIGHT_MODEL == Phong

#ifdef FEATURE_LIGHT_SHADOWMAPS
#if defined(FEATURE_LIGHT_INDIRECT) && FEATURE_LIGHT_INDIRECT > 1
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

            lightBounceSum += dot(offsetBounced, _surfaceNormal) < 0 ? intensityPostBounce : 0;
        }
    }
    lightBounceSum *= bounceAmount;

    return lightBounceSum * _light.lightIntensity * _light.lightColor;
}

#endif //FEATURE_LIGHT_INDIRECT
#define SHADOW_EDGE_FACE_SCALE 10.0

#if defined(FEATURE_LIGHT_SHADOWMAPS_AA) && FEATURE_LIGHT_SHADOWMAPS_AA > 1
    // MSAA offsets for shadow depth sampling:
    #if FEATURE_LIGHT_SHADOWMAPS_AA == 2
        static const float2 shadowSamplingOffsets[] =
        {
            { -0.5 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.5 / FEATURE_LIGHT_SHADOWMAPS_RES,  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES }
        };
    #elif FEATURE_LIGHT_SHADOWMAPS_AA == 4
        static const float2 shadowSamplingOffsets[] =
        {
            {  0, 0 },
            {  0, 0.5 / FEATURE_LIGHT_SHADOWMAPS_RES },
            { -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES },
        };
    #elif FEATURE_LIGHT_SHADOWMAPS_AA == 8
        static const float2 shadowSamplingOffsets[] =
        {
            {  0, 0 },
            {  0,  0.5 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0, -0.5 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.5 / FEATURE_LIGHT_SHADOWMAPS_RES, 0 },
            { -0.5 / FEATURE_LIGHT_SHADOWMAPS_RES, 0 },
            { -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, 0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
            { -0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, 0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
            {  0.25 / FEATURE_LIGHT_SHADOWMAPS_RES, -0.2 / FEATURE_LIGHT_SHADOWMAPS_RES },
        };
    #else
        #error "Shadow sampling count FEATURE_LIGHT_SHADOWMAPS_AA can only be 2 or 4"
    #endif
#endif

half CalculateShadowMapLightWeight(const in Light _light, const in float3 _worldPosition, const in float3 _surfaceNormal)
{
    // Determine shadow cascade for this pixel:
    const float cameraDist = length(_worldPosition - cameraPosition.xyz);
    const uint cascadeOffset = (uint)(2 * cameraDist / _light.shadowCascadeRange);
    const uint cascadeIdx = min(cascadeOffset, _light.shadowCascades);
    const uint shadowMapIdx = _light.shadowMapIdx + cascadeIdx;

    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:
    const float4 worldPosBiased = float4(_worldPosition + _surfaceNormal * _light.shadowBias + _light.shadowDepthBias, 1);

    // Transform pixel position to light's clip space, then to UV space:
    float4 shadowProj = mul(BufShadowMatrices[2 * shadowMapIdx], worldPosBiased);
    shadowProj /= shadowProj.w;
    const float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;

#if defined(FEATURE_LIGHT_SHADOWMAPS_AA) && FEATURE_LIGHT_SHADOWMAPS_AA > 1
    // Calculate shadow depth by averaging from multiple samples:
    static const half invShadowSampleCount = 1.0 / FEATURE_LIGHT_SHADOWMAPS_AA;
    half lightWeight = 0;
    for (uint i = 0; i < FEATURE_LIGHT_SHADOWMAPS_AA; ++i)
    {
        const float2 shadowSampleUv = shadowUv + shadowSamplingOffsets[i];
        const half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowSampleUv.x, shadowSampleUv.y, shadowMapIdx));
        lightWeight += shadowDepth > shadowProj.z ? 1 : 0;
    }
    lightWeight *= invShadowSampleCount;
#else
    // Calculate shadow depth from a single sample:
    const half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, shadowMapIdx));
    half lightWeight = shadowDepth > shadowProj.z ? 1 : 0;
#endif //FEATURE_LIGHT_SHADOWMAPS_AA
    
    // Fade shadows out near boundaries of UV/Depth space:
    if (_light.lightType == 2 && shadowMapIdx == _light.shadowCascades)
    {
        const half3 edgeUv = half3(shadowUv, shadowProj.z) * SHADOW_EDGE_FACE_SCALE;
        const half3 edgeMax = min(min(edgeUv, SHADOW_EDGE_FACE_SCALE - edgeUv), 1);
        const half k = 1 - min(min(edgeMax.x, edgeMax.y), edgeMax.z);
        lightWeight = lerp(lightWeight, 1.0, clamp(k, 0, 1));
    }

    return lightWeight;
}
#endif //FEATURE_LIGHT_SHADOWMAPS
#endif //FEATURE_LIGHT_SOURCES

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

            const half3 lightIntensity = CalculatePhongLighting(light, _worldPosition, _worldNormal);
            const half lightWeight = CalculateShadowMapLightWeight(light, _worldPosition, _surfaceNormal);
            totalLightIntensity += lightIntensity * lightWeight;

            #ifdef FEATURE_LIGHT_INDIRECT
            totalLightIntensity += CalculateIndirectLightScatter(light, _worldPosition, _surfaceNormal);
            #endif //FEATURE_LIGHT_INDIRECT
        }
        #else
        uint shadowMappedLightCount = 0;
        #endif //FEATURE_LIGHT_SHADOWMAPS
        // Simple light sources:
        for (i = shadowMappedLightCount; i < lightCount; ++i)
        {
            totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);
        }
    }
    #endif //FEATURE_LIGHT_SOURCES

    return totalLightIntensity;
}

#endif //FEATURE_LIGHT

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    // Recalculate UV from parallax map:
	float2 uv = inputBasic.uv;
	ApplyParallaxMap(uv, inputBasic.worldPosition, inputBasic.normal);

    #if FEATURE_ALBEDO_TEXTURE == 1
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, uv);
    #else
    half4 albedo = FEATURE_ALBEDO_COLOR;
    #endif //FEATURE_ALBEDO_TEXTURE == 1

    // Calculate normals from normal map:
	half3 normal = (half3)inputBasic.normal;
	ApplyNormalMap(normal, half3(0, 0, 1), half3(1, 0, 0), uv);

    #ifdef FEATURE_LIGHT
    // Apply basic phong lighting:
    const half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, normal, inputBasic.normal, uv);

    albedo *= half4(totalLightIntensity, 1);
    #endif //FEATURE_LIGHT

    // Return final color:
    return albedo;
};

#ifdef VARIANT_EXTENDED
half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0
{
    // Recalculate UV from parallax map:
	float2 uv = inputBasic.uv;
	ApplyParallaxMap(uv, inputBasic.worldPosition, inputBasic.normal);

    #if FEATURE_ALBEDO_TEXTURE == 1
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, uv);
    #else
    half4 albedo = FEATURE_ALBEDO_COLOR;
    #endif //FEATURE_ALBEDO_TEXTURE == 1

    // Calculate normals from normal map:
	half3 normal = (half3)inputBasic.normal;
	ApplyNormalMap(normal, inputExt.tangent, inputExt.binormal, uv);

    #ifdef FEATURE_LIGHT
    // Apply basic phong lighting:
    const half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, normal, inputBasic.normal, uv);

    albedo *= half4(totalLightIntensity, 1);
    #endif //FEATURE_LIGHT

    // Return final color:
    return albedo;
};
#endif //VARIANT_EXTENDED
