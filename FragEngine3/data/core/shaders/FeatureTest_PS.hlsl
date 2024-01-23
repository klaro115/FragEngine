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

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
};

/******************* BUFFERS: ******************/

// ResSetCamera:

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

StructuredBuffer<Light> BufLights : register(ps, t0);   // Buffer containing an array of light source data. Number of lights is given in 'CBGlobal.lightCount'.

Texture2DArray<half> TexShadowMaps : register(ps, t1);
SamplerState SamplerShadowMaps : register(ps, s0);

// ResSetBound:

Texture2D<half4> TexMain : register(ps, t2);
Texture2D<half3> TexNormal : register(ps, t3);
SamplerState SamplerMain : register(s1);

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
#define SHADOW_EDGE_FACE_SCALE 10.0

half3 CalculateAmbientLight(in float3 _worldNormal)
{
    half dotY = (half)dot(_worldNormal, float3(0, 1, 0));
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

        // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:
        float4 worldPosBiased = float4(_worldPosition + _worldNormal * light.shadowBias, 1);

        // Transform pixel position to light's clip space, then to UV space:
        float4 shadowProj = mul(light.mtxShadowWorld2Clip, worldPosBiased);
        shadowProj /= shadowProj.w;
        float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;
        
        // Load corresponding depth value from shadow texture array:
        half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, light.shadowMapIdx));
        half lightWeight = shadowDepth > shadowProj.z ? 1 : 0;

        // Fade shadows out near boundaries of UV/Depth space:
        if (light.lightType == 2)
        {
            half3 edgeUv = half3(shadowUv, shadowProj.z) * SHADOW_EDGE_FACE_SCALE;
            half3 edgeMax = min(min(edgeUv, SHADOW_EDGE_FACE_SCALE - edgeUv), 1);
            half k = 1 - min(min(edgeMax.x, edgeMax.y), edgeMax.z);
            lightWeight = lerp(lightWeight, 1.0, clamp(k, 0, 1));
        }

        totalLightIntensity += lightIntensity * lightWeight;
    }
    // Simple light sources:
    for (i = shadowMappedLightCount; i < lightCount; ++i)
    {
        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);
    }

    return totalLightIntensity;
}

/******************* NORMALS: ******************/

half3 UnpackNormalMap(in half3 _texNormal)
{
    // Unpack direction vector from normal map colors:
    return half3(_texNormal.x * 2 - 1, _texNormal.z, _texNormal.y * 2 - 1); // NOTE: Texture normals are expected to be in OpenGL standard.
}

half3 ApplyNormalMap(in half3 _worldNormal, in half3 _worldTangent, in half3 _worldBinormal, in half3 _texNormal)
{
    _texNormal = UnpackNormalMap(_texNormal);

    // Create rotation matrix, projecting from flat surface (UV) space to surface in world space:
    half3x3 mtxNormalRot =
    {
        _worldBinormal.x, _worldNormal.x, _worldTangent.x,
        _worldBinormal.y, _worldNormal.y, _worldTangent.y,
        _worldBinormal.z, _worldNormal.z, _worldTangent.z,
    };
    half3 normal = mul(mtxNormalRot, _texNormal);
    return normal;
}

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, inputBasic.uv);

    // Calculate normals from normal map:
    half3 normal = TexNormal.Sample(SamplerMain, inputBasic.uv);
    normal = ApplyNormalMap((half3)inputBasic.normal, half3(0, 0, 1), half3(1, 0, 0), normal);

    // Apply basic phong lighting:
    half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, inputBasic.normal);

    albedo *= half4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};

half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0
{
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, inputBasic.uv);

    // Calculate normals from normal map:
    half3 normal = TexNormal.Sample(SamplerMain, inputBasic.uv);
    normal = ApplyNormalMap((half3)inputBasic.normal, (half3)inputExt.tangent, (half3)inputExt.binormal, normal);

    // Apply basic phong lighting:
    half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, (float3)normal);

    albedo *= half4(totalLightIntensity, 1);

    // Return final color:
    return albedo;
};
