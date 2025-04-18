//#pragma pack_matrix( column_major )

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

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
};

cbuffer CBShadowMapVisualizer : register(b4)
{
    uint shadowMapIdx;
    float3 padding;
}

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

/****************** RESOURCES: *****************/

Texture2DArray<half> TexShadowMaps : register(ps, t1);
SamplerState SamplerShadowMaps : register(ps, s0);

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    const float3 uv = float3(inputBasic.uv, shadowMapIdx);

    const float shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, uv);

    return half4(shadowDepth, shadowDepth, shadowDepth, 1);
};
