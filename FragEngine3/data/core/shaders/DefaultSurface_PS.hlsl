#pragma pack_matrix( column_major )

/****************** CONSTANTS: *****************/

cbuffer CBGlobal : register(b0)
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

cbuffer CBObject : register(b1)
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

StructuredBuffer<Light> BufLights : register(ps, t0);   // Buffer containing an array of light source data. Number of lights is given in 'CBGlobal.lightCount'.

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

struct VertexOutput_Extended
{
    float3 tangent : NORMAL1;
    float3 binormal : NORMAL2;
    float2 uv2 : TEXCOORD1;
};

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    half4 albedo = {1, 1, 1, 1};

    // Apply basic phong lighting:
    half3 totalLightIntensity = ambientLight;
    for (uint i = 0; i < lightCount; ++i)
    {
        Light light = BufLights[i];

        half4 lightIntens = (half4)light.lightIntensity;
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
            if (light.lightType == 1 && dot(light.lightDirection, lightRayDir) < light.lightSpotAngleAcos)
            {
                lightIntens = float4(0, 0, 0, 0);
            }
        }

        float lightDot = max(-dot(lightRayDir, inputBasic.normal), 0);
        totalLightIntensity += lightIntens.xyz * lightDot;
    }
    albedo *= half4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};
