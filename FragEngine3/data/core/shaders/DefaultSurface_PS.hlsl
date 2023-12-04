/****************** CONSTANTS: *****************/

cbuffer Global : register(b0)
{
	// Camera parameters:
    uint resolutionX;           // Render target width, in pixels.
    uint resolutionY;           // Render target height, in pixels.
    float nearClipPlane;        // Camera's near clipping plane distance.
    float farClipPlane;         // Camera's far clipping plane distance.

    // Camera vectors & matrices:
    float3 cameraPosition;      // Camera position, in world space.
    float3 cameraDirection;     // Camera forward facing direction, in world space.
    float4x4 mtxCamera;         // Camera's full projection matrix, transforming from world space to viewport pixel coordinates.

    // Lighting:
    uint lightCount;
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

StructuredBuffer<Light> BufLights : register(ps, t0);   // Buffer containing an array of light source data. Number of lights is given in 'Global.lightCount'.

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_Position;
    float3 worldPosition : POSITION;
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

float4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    float4 albedo = {1, 1, 1, 1};

    // Apply basic phong lighting:
    float4 totalLightIntensity = {0, 0, 0, 0};
    for (uint i = 0; i < lightCount; ++i)
    {
        Light light = BufLights[i];

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

        float lightDot = max(dot(lightRayDir, inputBasic.normal), 0);
        totalLightIntensity += lightDot * lightIntens;
    }
    albedo *= totalLightIntensity;

    // Return final color:
    return albedo;
};
