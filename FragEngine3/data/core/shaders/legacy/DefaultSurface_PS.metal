//#pragma pack_matrix( column_major )
#include <metal_stdlib>
using namespace metal;

/****************** CONSTANTS: *****************/

struct CBScene
{
    // Scene lighting:
    float4 ambientLightLow;         // Ambient light color and intensity coming from bottom-up.
    float4 ambientLightMid;         // Ambient light color and intensity coming from all sides.
    float4 ambientLightHigh;        // Ambient light color and intensity coming from top-down.
    float shadowFadeStart;          // Percentage of the shadow distance in projection space where they start fading out.
};

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

struct CBObject
{
    float4x4 mtxLocal2World;    // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;       // World space position of the object.
    float boundingRadius;       // Bounding sphere radius of the object.
};

/******************* BUFFERS: ******************/

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

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];
};

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

/******************* SHADERS: ******************/

half3 CalculateAmbientLight(
    device const CBScene& cbScene,
    float3 _normal)
{
    half dotY = (half)dot(_normal, float3(0, 1, 0));
    half wLow = max(-dotY, (half)0);
    half wHigh = max(dotY, (half)0);
    half wMid = 1 - wHigh - wLow;
    return (wLow * (half4)cbScene.ambientLightLow + wHigh * (half4)cbScene.ambientLightHigh + wMid * (half4)cbScene.ambientLightMid).xyz;
}

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
            float3 lightOffset = inputBasic.worldPosition - light.lightPosition;
            lightIntens /= (half)dot(lightOffset, lightOffset);
            lightRayDir = normalize(lightOffset);

            // Spot light angle:
            if (light.lightType == 1 && dot(light.lightDirection, lightRayDir) < light.lightSpotMinDot)
            {
                lightIntens = half3(0, 0, 0);
            }
        }

        half lightDot = max(-(half)dot(lightRayDir, inputBasic.normal), (half)0.0);
        totalLightIntensity += lightIntens.xyz * lightDot;
    }
    albedo *= half4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};
