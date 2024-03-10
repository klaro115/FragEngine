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
#define FEATURE_LIGHT_INDIRECT 5

// Variants:
#define VARIANT_EXTENDED                        // Whether to always create a shader variant using extended surface data
#define VARIANT_BLENDSHAPES                     // Whether to always create a shader variant using blend shape data
#define VARIANT_ANIMATED                        // Whether to always create a shader variant using bone animation data    

#if FEATURE_ALBEDO_TEXTURE == 0
    #ifndef FEATURE_ALBEDO_COLOR
        #define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)
    #endif
#endif

/****************** CONSTANTS: *****************/

#ifdef FEATURE_LIGHT_AMBIENT
// Constant buffer containing all scene-wide settings:
cbuffer CBScene : register(b0)
{
    // Scene lighting:
    float4 ambientLightLow;         // Ambient light color and intensity coming from bottom-up.
    float4 ambientLightMid;         // Ambient light color and intensity coming from all sides.
    float4 ambientLightHigh;        // Ambient light color and intensity coming from top-down.
    float shadowFadeStart;          // Percentage of the shadow distance in projection space where they start fading out.
};
#endif //FEATURE_LIGHT_AMBIENT

#if defined(FEATURE_LIGHT_SOURCES) || defined(FEATURE_PARALLAX)
// Constant buffer containing all settings that apply for everything drawn by currently active camera:
cbuffer CBCamera : register(b1)
{
    // Camera vectors & matrices:
    float4x4 mtxWorld2Clip;         // Camera's full projection matrix, transforming from world space to clip space coordinates.
    float4 cameraPosition;          // Camera position, in world space.
    float4 cameraDirection;         // Camera forward facing direction, in world space.
    float4x4 mtxCameraMotion;       // Camera movement matrix, encoding motion/transformation from previous to current frame.

	// Camera parameters:
    uint cameraIdx;                 // Index of the currently drawing camera.
    uint resolutionX;               // Render target width, in pixels.
    uint resolutionY;               // Render target height, in pixels.
    float nearClipPlane;            // Camera's near clipping plane distance.
    float farClipPlane;             // Camera's far clipping plane distance.

    // Per-camera lighting:
    uint lightCount;                // Total number of lights affecting this camera.
    uint shadowMappedLightCount;    // Total number of lights that have a layer of the shadow map texture array assigned.
};
#endif //FEATURE_LIGHT_SOURCES

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
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
};

StructuredBuffer<Light> BufLights : register(ps, t0);               // Buffer containing an array of light source data. Number of lights is given in 'CBGlobal.lightCount'.
#endif

#ifdef FEATURE_LIGHT_SHADOWMAPS
Texture2DArray<half> TexShadowMaps : register(ps, t1);
StructuredBuffer<float4x4> BufShadowMatrices : register(ps, t2);    // Buffer containing an array of projectionm matrices for shadow maps, transforming world position to clip space.
SamplerState SamplerShadowMaps : register(ps, s0);
#endif

//#if defined(FEATURE_LIGHT_SHADOWMAPS) && defined(FEATURE_LIGHT_INDIRECT) && FEATURE_LIGHT_INDIRECT > 1
//Texture2DArray<half4> TexDiffusionMaps : register(ps, t3);
//#endif

// ResSetBound:

#if FEATURE_ALBEDO_TEXTURE == 1
Texture2D<half4> TexMain : register(ps, t3);
#endif //FEATURE_ALBEDO_TEXTURE == 1

#ifdef FEATURE_NORMALS
Texture2D<half3> TexNormal : register(ps, t4);
#endif //FEATURE_NORMALS

#ifdef FEATURE_PARALLAX
Texture2D<half> TexParallax : register(ps, t5);
#endif //FEATURE_PARALLAX

#ifdef FEATURE_LIGHT_LIGHTMAPS
Texture2D<half3> TexLightmap : register(ps, t6);
#endif //FEATURE_LIGHT_LIGHTMAPS

#if FEATURE_ALBEDO_TEXTURE == 1 || defined(FEATURE_NORMALS) || defined(FEATURE_PARALLAX) || defined(FEATURE_LIGHT_LIGHTMAPS)
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

#ifdef FEATURE_LIGHT_AMBIENT
half3 CalculateAmbientLight(const in float3 _worldNormal)
{
    const half dotY = (half)dot(_worldNormal, float3(0, 1, 0));
    const half wLow = max(-dotY, 0);
    const half wHigh = max(dotY, 0);
    const half wMid = 1.0 - wHigh - wLow;
    return (wLow * (half4)ambientLightLow + wHigh * (half4)ambientLightHigh + wMid * (half4)ambientLightMid).xyz;
}
#endif //FEATURE_LIGHT_AMBIENT

#ifdef FEATURE_LIGHT_LIGHTMAP
half3 CalculateLightmaps(const in float2 _uv)
{
    return TexLightmap.Sample(SamplerMain, _uv);
}
#endif //FEATURE_LIGHT_LIGHTMAP

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
            const half depth = TexShadowMaps.Sample(SamplerShadowMaps, half3(uvX, uvY, _light.shadowMapIdx));
            const half4 posClipSpace = half4(uvX * 2 - 1, 1 - uvY * 2, depth, 1);

            const float3 posWorld = mul(mtxShadowClip2World, posClipSpace).xyz;
            const float3 lightOffset = _worldPosition - posWorld;

            // Determine approximate lighting at sampled point, pre-bounce:
            const float3 offsetPreBounce = posWorld - _light.lightPosition;
            const float distSqPreBounce = dot(offsetPreBounce, offsetPreBounce);
            const float intensityPreBounce = 1.0 / distSqPreBounce;

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

half CalculateShadowMapLightWeight(const in Light _light, const in float3 _worldPosition, const in float3 _surfaceNormal)
{
    // Determine shadow cascade for this pixel:
    const float cameraDist = length(_worldPosition - cameraPosition.xyz);
    const uint cascadeOffset = (uint)(2 * cameraDist / _light.shadowCascadeRange);
    const uint cascadeIdx = min(cascadeOffset, _light.shadowCascades);
    const uint shadowMapIdx = _light.shadowMapIdx + cascadeIdx;

    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:
    const float4 worldPosBiased = float4(_worldPosition + _surfaceNormal * _light.shadowBias, 1);

    // Transform pixel position to light's clip space, then to UV space:
    float4 shadowProj = mul(BufShadowMatrices[2 * shadowMapIdx], worldPosBiased);
    shadowProj /= shadowProj.w;
    const float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;
    
    // Load corresponding depth value from shadow texture array:
    const half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, shadowMapIdx));
    half lightWeight = shadowDepth > shadowProj.z ? 1 : 0;

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
    #ifdef FEATURE_LIGHT_AMBIENT
    half3 totalLightIntensity = CalculateAmbientLight(_worldNormal);
    #else
    half3 totalLightIntensity = half3(0, 0, 0);
    #endif //FEATURE_LIGHT_AMBIENT

    // Apply light maps:
    #ifdef FEATURE_LIGHT_LIGHTMAP
    totalLightIntensity += CalculateLightmaps(_uv);
    #endif

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

#ifdef FEATURE_NORMALS
/******************* NORMALS: ******************/

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
#endif //FEATURE_NORMALS

#ifdef FEATURE_PARALLAX
/****************** PARALLAX: ******************/

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

half2 ApplyParallaxMap(const in float3 _worldPosition, const in float3 _surfaceNormal, const half2 _uv)
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
#endif

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    #ifdef FEATURE_PARALLAX
    // Recalculate UV from parallax map:
    float2 uv = ApplyParallaxMap(inputBasic.worldPosition, inputBasic.normal, inputBasic.uv);
    #else
    float2 uv = inputBasic.uv;
    #endif //FEATURE_PARALLAX

    #if FEATURE_ALBEDO_TEXTURE == 1
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, uv);
    #else
    half4 albedo = FEATURE_ALBEDO_COLOR;
    #endif //FEATURE_ALBEDO_TEXTURE == 1

    #ifdef FEATURE_NORMALS
    // Calculate normals from normal map:
    half3 normal = TexNormal.Sample(SamplerMain, uv);
    normal = ApplyNormalMap(inputBasic.normal, half3(0, 0, 1), half3(1, 0, 0), normal);
    #else
    half3 normal = inputBasic.normal;
    #endif //FEATURE_NORMALS

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
    #ifdef FEATURE_PARALLAX
    // Recalculate UV from parallax map:
    float2 uv = ApplyParallaxMap(inputBasic.worldPosition, inputBasic.normal, inputBasic.uv);
    #else
    float2 uv = inputBasic.uv;
    #endif //FEATURE_PARALLAX

    #if FEATURE_ALBEDO_TEXTURE == 1
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, uv);
    #else
    half4 albedo = FEATURE_ALBEDO_COLOR;
    #endif //FEATURE_ALBEDO_TEXTURE == 1

    #ifdef FEATURE_NORMALS
    // Calculate normals from normal map:
    half3 normal = TexNormal.Sample(SamplerMain, uv);
    normal = ApplyNormalMap(inputBasic.normal, inputExt.tangent, inputExt.binormal, normal);
    #else
    half3 normal = inputBasic.normal;
    #endif //FEATURE_NORMALS

    #ifdef FEATURE_LIGHT
    // Apply basic phong lighting:
    const half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, normal, inputBasic.normal, uv);

    albedo *= half4(totalLightIntensity, 1);
    #endif //FEATURE_LIGHT

    // Return final color:
    return albedo;
};
#endif //VARIANT_EXTENDED
