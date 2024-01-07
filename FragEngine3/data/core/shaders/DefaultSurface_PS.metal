//#pragma pack_matrix( column_major )
#include <metal_stdlib>
using namespace metal;

/****************** CONSTANTS: *****************/

struct CBGlobal
{
    // Camera vectors & matrices:
    float4x4 mtxWorld2Clip;     // Camera's full projection matrix, transforming from world space to clip space coordinates.
    float4 cameraPosition;      // Camera position, in world space.
    float4 cameraDirection;     // Camera forward facing direction, in world space.

	// Camera parameters:
    uint resolutionX;           // Render target width, in pixels.
    uint resolutionY;           // Render target height, in pixels.
    float nearClipPlane;        // Camera's near clipping plane distance.
    float farClipPlane;         // Camera's far clipping plane distance.

    // Lighting:
    float3 ambientLight;
    uint lightCount;
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
    float lightSpotAngleAcos;
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position                     [[ position ]];
    float3 worldPosition;
    float3 normal;
    float2 uv;
};

//struct VertexOutput_Extended
//{
//    float3 tangent;
//    float3 binormal;
//    float2 uv2;
//};

/******************* SHADERS: ******************/

float4 fragment Main_Pixel(
    VertexOutput_Basic inputBasic       [[ stage_in ]],
    device const CBGlobal& cbGlobal     [[ buffer( 1 ) ]],
    device const CBObject& cbObject     [[ buffer( 2 ) ]],
    device const Light* BufLights       [[ buffer( 3 ) ]])
{
    float4 albedo = {1, 1, 1, 1};

    // Apply basic phong lighting:
    float3 totalLightIntensity = cbGlobal.ambientLight;
    for (uint i = 0; i < cbGlobal.lightCount; ++i)
    {
        device const Light& light = BufLights[i];

        float4 lightIntens = light.lightIntensity;
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
            lightIntens /= dot(lightOffset, lightOffset);
            lightRayDir = normalize(lightOffset);

            // Spot light angle:
            if (light.lightType == 1 && dot(light.lightDirection, lightRayDir) < light.lightSpotAngleAcos)
            {
                lightIntens = float4(0, 0, 0, 0);
            }
        }

        float lightDot = max(-dot(lightRayDir, inputBasic.normal), (float)0.0);
        totalLightIntensity += lightIntens.xyz * lightDot;
    }
    albedo *= float4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};
