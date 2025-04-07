#pragma pack_matrix( column_major )

/****************** CONSTANTS: *****************/

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

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

/******************* SHADERS: ******************/

#define MIN_ALPHA 0.05

static const float4 colorGroundV = { 0.5, 0.3, 0.2, 1.0 };
static const float4 colorGroundH = { 0.7, 0.6, 0.5, 1.0 };
static const float4 colorSkyH = { 0.98, 0.98, 1.0, 1.0 };
static const float4 colorSkyV = { 0.4, 0.4, 1.0, 1.0 };

static const float midGradientWidth = 0.05;
static const float invMidGradientWidth = 0.5 / midGradientWidth;

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    const float3 pixelOffset = inputBasic.worldPosition - cameraPosition.xyz;
    const float3 pixelDir = normalize(pixelOffset);

    const float dotY = dot(pixelDir, float3(0, 1, 0));
    const float dotX = 1.0 - abs(dotY);
    float kUp = 1 - max(dotY, 0);
    kUp = 1 - kUp * kUp;
    const float kDown = max(-dotY, 0);

    const float4 colorGround = lerp(colorGroundH, colorGroundV, kDown);
    const float4 colorSky = lerp(colorSkyH, colorSkyV, kUp);

    float4 colorFinal;
    if (kUp > midGradientWidth)
    {
        colorFinal = colorSky;
    }
    else if (kDown > midGradientWidth)
    {
        colorFinal = colorGround;
    }
    else
    {
        const float kMid = (midGradientWidth - kDown + kUp) * invMidGradientWidth;
        colorFinal = lerp(colorGround, colorSky, smoothstep(0, 1, kMid));
    }
    return (half4)colorFinal;
};
