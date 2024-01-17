#pragma pack_matrix( column_major )

/****************** CONSTANTS: *****************/

// Constant buffer containing all scene-wide settings:
cbuffer CBScene : register(b0)
{
    // Scene lighting:
    float4 ambientLightLow;         // Ambient light color and intensity coming from bottom-up.
    float4 ambientLightMid;         // Ambient light color and intensity coming from all sides.
    float4 ambientLightHigh;        // Ambient light color and intensity coming from top-down.
    float shadowFadeStart;          // Percentage of the shadow distance in projection space where they start fading out.
};

// Constant buffer containing all settings that apply for everything drawn by currently active camera:
cbuffer CBCamera : register(b1)
{
    // Camera vectors & matrices:
    float4x4 mtxWorld2Clip;         // Camera's full projection matrix, transforming from world space to clip space coordinates.
    float4 cameraPosition;          // Camera position, in world space.
    float4 cameraDirection;         // Camera forward facing direction, in world space.

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

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
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
    float4x4 mtxShadowWorld2Clip;
    uint shadowMapIdx;
    float lightMaxRange;
};

StructuredBuffer<Light> BufLights : register(ps, t0);   // Buffer containing an array of light source data. Number of lights is given in 'CBGlobal.lightCount'.

Texture2DArray<half> TexShadowMaps : register(ps, t1);
SamplerState SamplerShadowMaps : register(ps, s0);

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

/****************** LIGHTING: ******************/

static const float LIGHT_NEAR_CLIP_PLANE = 0.001;
static const float LIGHT_BIAS = -0.03;

half3 CalculateAmbientLight(float3 _normal)
{
    half dotY = (half)dot(_normal, float3(0, 1, 0));
    half wLow = max(-dotY, 0);
    half wHigh = max(dotY, 0);
    half wMid = 1.0 - wHigh - wLow;
    return (wLow * (half4)ambientLightLow + wHigh * (half4)ambientLightHigh + wMid * (half4)ambientLightMid).xyz;
}

half3 CalculatePhongLighting(in Light _light, in float3 _worldPosition, in float3 _worldNormal)
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
        float3 lightOffset = _worldPosition - _light.lightPosition;
        lightIntens /= (half)dot(lightOffset, lightOffset);
        lightRayDir = normalize(lightOffset);

        // Spot light angle:
        if (_light.lightType == 1 && dot(_light.lightDirection, lightRayDir) < _light.lightSpotMinDot)
        {
            lightIntens = half3(0, 0, 0);
        }
    }

    half lightDot = max(-(half)dot(lightRayDir, _worldNormal), 0.0);
    return lightIntens.xyz * lightDot;
}

half3 CalculateTotalLightIntensity(in float3 _worldPosition, in float3 _worldNormal)
{
    half3 totalLightIntensity = CalculateAmbientLight(_worldNormal);

    // Shadow-casting light sources:
    for (uint i = 0; i < shadowMappedLightCount; ++i)
    {
        Light light = BufLights[i];

        half3 lightIntensity = CalculatePhongLighting(light, _worldPosition, _worldNormal);

        // Transform pixel position to light's clip space, then to UV space:
        float4 shadowProj = mul(light.mtxShadowWorld2Clip, float4(_worldPosition + light.lightDirection * LIGHT_BIAS, 1));
        shadowProj /= shadowProj.w;
        float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;
        
        // Load corresponding depth value from shadow texture array:
        half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, light.shadowMapIdx));

        lightIntensity *= shadowProj.z < shadowDepth ? 1 : 0;           //TODO [later]: fade out shadow beyond 90% depth.
        totalLightIntensity += lightIntensity;
    }
    // Simple light sources:
    for (i = shadowMappedLightCount; i < lightCount; ++i)
    {
        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);
    }

    return totalLightIntensity;
}

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    half4 albedo = {1, 1, 1, 1};

    // Apply basic phong lighting:
    half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, inputBasic.normal);

    albedo *= half4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};
