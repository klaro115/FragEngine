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

// Opaque geometry:
Texture2D<half4> TexOpaqueColor : register(ps, t4);
Texture2D<float2> TexOpaqueDepth : register(ps, t5);

// Transparent geometry:
Texture2D<half4> TexTransparentColor : register(ps, t6);
Texture2D<float2> TexTransparentDepth : register(ps, t7);

// UI Overlay:
Texture2D<half4> TexUIColor : register(ps, t8);

/******************* SHADERS: ******************/

PixelOutput Main_Pixel(in VertexOutput_Basic inputBasic)
{
    // Determine source pixel location from fullscreen quad's UV:
    const int3 posPixel = int3(inputBasic.uv * float2(resolutionX, resolutionY), 0);

    // Load pixel color and depth for all textures:
    const half4 colOpaque = TexOpaqueColor.Load(posPixel);
    const float depthOpaque = TexOpaqueDepth.Load(posPixel).r;

    const half4 colTransparent = TexTransparentColor.Load(posPixel);
    const float depthTransparent = TexTransparentDepth.Load(posPixel).r;

    const half4 colUI = TexUIColor.Load(posPixel);

    // Composite geometry: (opaque & transparent)
    const half k = depthTransparent > depthOpaque ? colTransparent.w : 0;
    const half4 colGeometry = lerp(colOpaque, colTransparent, k);
    const float depthGeometry = min(depthTransparent, depthOpaque);

    // Overlay UI:
    const half alphaFinal = clamp(colGeometry.w + colUI.w, 0, 1);
    const half4 colFinal = half4(lerp(colGeometry.xyz, colUI.xyz, colUI.w), alphaFinal);
    const float depthFinal = colUI.w <= 0.001 ? depthGeometry : 0;

    // Assemble final output:
    PixelOutput o;
    o.color = colFinal;
    o.depth = depthFinal;
    return o;
}
