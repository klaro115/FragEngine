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

/***************** PIXEL OUTPUT: ***************/

struct PixelOutput
{
    half4 color : SV_Target0;
    float depth : SV_Depth;
};

/****************** TEXTURES: ******************/

// Scene geometry:
Texture2D<half4> TexSceneColor : register(ps, t4);
Texture2D<float2> TexSceneDepth : register(ps, t5);

// UI Overlay:
Texture2D<half4> TexUIColor : register(ps, t6);

/******************* SHADERS: ******************/

PixelOutput Main_Pixel(in VertexOutput_Basic inputBasic)
{
    // Determine source pixel location from fullscreen quad's UV:
    const int3 posPixel = int3(inputBasic.uv * float2(resolutionX, resolutionY), 0);

    // Load pixel color and depth for all textures:
    const half4 colScene = TexSceneColor.Load(posPixel);
    const float depthScene = TexSceneDepth.Load(posPixel).r;

    const half4 colUI = TexUIColor.Load(posPixel);

    // Overlay UI:
    const half alphaFinal = clamp(colScene.w + colUI.w, 0, 1);
    const half4 colFinal = half4(lerp(colScene.xyz, colUI.xyz, colUI.w), alphaFinal);
    const float depthFinal = colUI.w <= 0.001 ? depthScene : 0;

    // Assemble final output:
    PixelOutput o;
    o.color = colFinal;
    o.depth = depthFinal;
    return o;
}
