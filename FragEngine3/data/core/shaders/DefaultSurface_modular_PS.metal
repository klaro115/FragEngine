#include <metal_stdlib>
using namespace metal;

/******************* DEFINES: ******************/

// Albedo:
#define FEATURE_ALBEDO_TEXTURE 1                // Whether to initialize albedo color from main texture. If false, a color literal is used
#define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)  // Color literal from which albedo may be initialized

// Normals:
#define FEATURE_NORMALS                         // Whether to use normal maps in all further shading

// Lighting:
#define FEATURE_LIGHT                           // Whether to apply lighting
#define FEATURE_LIGHT_AMBIENT                   // Whether to add directional ambient intensity to base lighting
#define FEATURE_LIGHT_LIGHTMAPS                 // Whether to add light map intensity to base lighting
#define FEATURE_LIGHT_SOURCES                   // Whether to use light sources from the scene to light up the object
#define FEATURE_LIGHT_MODEL Phong               // Which lighting model to use for light sources. Default is "Phong"
#define FEATURE_LIGHT_SHADOWMAPS                // Whether to use shadow maps to mask out light rays coming from light sources

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
struct CBScene
{
    // Scene lighting:
    float4 ambientLightLow;         // Ambient light color and intensity coming from bottom-up.
    float4 ambientLightMid;         // Ambient light color and intensity coming from all sides.
    float4 ambientLightHigh;        // Ambient light color and intensity coming from top-down.
    float shadowFadeStart;          // Percentage of the shadow distance in projection space where they start fading out.
};
#endif //FEATURE_LIGHT_AMBIENT

#ifdef FEATURE_LIGHT_SOURCES
// Constant buffer containing all settings that apply for everything drawn by currently active camera:
struct CBCamera
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

struct CBObject
{
    float4x4 mtxLocal2World;    // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;       // World space position of the object.
    float boundingRadius;       // Bounding sphere radius of the object.
};

/****************** RESOURCES: *****************/

#ifdef FEATURE_LIGHT_SOURCES
struct Light
{
    float3 lightColor;
    float lightIntensity;
    float3 lightPosition;
    uint lightType;
    float3 lightDirection;
    float lightSpotMinDot;
    float4x4 mtxShadowWorld2Clip;
    uint shadowMapIdx;
    float shadowBias;
};
#endif

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];
};

#ifdef VARIANT_EXTENDED
struct VertexOutput_Extended
{
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];

    float3 tangent          [[ user(tangent) ]];
    float3 binormal         [[ user(binormal) ]];
    float2 uv2              [[ user(uv2) ]];
};
#else
#endif //VARIANT_EXTENDED

#ifdef FEATURE_LIGHT
/****************** LIGHTING: ******************/

half3 CalculateAmbientLight(
    device const CBScene& cbScene,
    const float3& _normal)
{
    const half dotY = (half)dot(_normal, float3(0, 1, 0));
    const half wLow = max(-dotY, (half)0);
    const half wHigh = max(dotY, (half)0);
    const half wMid = 1 - wHigh - wLow;
    return (wLow * (half4)cbScene.ambientLightLow + wHigh * (half4)cbScene.ambientLightHigh + wMid * (half4)cbScene.ambientLightMid).xyz;
}

#else
#endif //FEATURE_LIGHT

#ifdef FEATURE_NORMALS
/******************* NORMALS: ******************/

half3 UnpackNormalMap(const half3& _texNormal)
{
    // Unpack direction vector from normal map colors:
    return half3(_texNormal.x * 2 - 1, _texNormal.z, _texNormal.y * 2 - 1); // NOTE: Texture normals are expected to be in OpenGL standard.
}

half3 ApplyNormalMap(const half3& _worldNormal, const half3& _worldTangent, const half3& _worldBinormal, half3 _texNormal)
{
    _texNormal = UnpackNormalMap(_texNormal);

    // Create rotation matrix, projecting from flat surface (UV) space to surface in world space:
    const half3x3 mtxNormalRot =
    {
        _worldBinormal.x, _worldNormal.x, _worldTangent.x,
        _worldBinormal.y, _worldNormal.y, _worldTangent.y,
        _worldBinormal.z, _worldNormal.z, _worldTangent.z,
    };
    const half3 normal = mtxNormalRot * _texNormal;
    return normal;
}
#else
#endif //FEATURE_NORMALS

/******************* SHADERS: ******************/

half4 fragment Main_Pixel(
    VertexOutput_Basic inputBasic                       [[ stage_in ]],
    device const CBScene& cbScene                       [[ buffer( 0 ) ]],
    device const CBCamera& cbCamera                     [[ buffer( 1 ) ]],
    device const CBObject& cbObject                     [[ buffer( 2 ) ]],
    device const Light* BufLights                       [[ buffer( 3 ) ]],
    texture2d_array<half, access::sample> TexShadowMaps [[ texture( 0 ) ]])
{
    half4 albedo = {1, 1, 1, 1};

    // Apply basic phong lighting:
    half3 totalLightIntensity = CalculateAmbientLight(cbScene, inputBasic.normal);
    for (uint i = 0; i < cbCamera.lightCount; ++i)
    {
        device const Light& light = BufLights[i];

        half3 lightIntens = (half3)(light.lightColor * light.lightIntensity);
        float3 lightRayDir;

        // Directional light:
        if (light.lightType == 2)
        {
            lightRayDir = light.lightDirection;
        }
        // Point or Spot light:
        else
        {
            const float3 lightOffset = inputBasic.worldPosition - light.lightPosition;
            lightIntens /= (half)dot(lightOffset, lightOffset);
            lightRayDir = normalize(lightOffset);

            // Spot light angle:
            if (light.lightType == 1 && dot(light.lightDirection, lightRayDir) < light.lightSpotMinDot)
            {
                lightIntens = half3(0, 0, 0);
            }
        }

        const half lightDot = max(-(half)dot(lightRayDir, inputBasic.normal), (half)0.0);
        totalLightIntensity += lightIntens.xyz * lightDot;
    }
    albedo *= half4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};
