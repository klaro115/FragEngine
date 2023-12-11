#pragma pack_matrix( column_major )

/****************** CONSTANTS: *****************/

cbuffer CBGlobal : register(b0)
{
	// Camera vectors & matrices:
    float4x4 mtxCamera;         // Camera's full projection matrix, transforming from world space to clip space coordinates.
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
Texture2D<half4> TexOpaqueColor : register(ps, t1);
Texture2D<float> TexOpaqueDepth : register(ps, t2);

// Transparent geometry:
Texture2D<half4> TexTransparentColor : register(ps, t3);
Texture2D<float> TexTransparentDepth : register(ps, t4);

// UI Overlay:
Texture2D<half4> TexUI : register(ps, t5);

/******************* SHADERS: ******************/

static const float PI = 3.141592653;

PixelOutput Main_Pixel(in VertexOutput_Basic inputBasic)
{
    // Determine source pixel location from fullscreen quad's UV:
    int3 posPixel = int3(inputBasic.uv * float2(resolutionX, resolutionY), 0);

    // Load pixel color and depth for all textures:
    half4 colOpaque = TexOpaqueColor.Load(posPixel);
    float depthOpaque = TexOpaqueDepth.Load(posPixel);

    half4 colTransparent = TexTransparentColor.Load(posPixel);
    float depthTransparent = TexTransparentDepth.Load(posPixel);

    half4 colUI = TexUI.Load(posPixel);

    // Composite geometry: (opaque & transparent)
    float k = depthTransparent > depthOpaque ? colTransparent.w : 0;
    half4 colGeometry = lerp(colOpaque, colTransparent, k);
    float depthGeometry = min(depthTransparent, depthOpaque);

    // Overlay UI:
    half alphaFinal = clamp(colGeometry.w + colUI.w, 0, 1);
    half4 colFinal = float4(lerp(colGeometry.xyz, colUI.xyz, colUI.w), alphaFinal);
    float depthFinal = colUI.w <= 0.001 ? depthGeometry : 0;
    
    // Assemble final output:
    PixelOutput o;
    o.color = colFinal;
    o.depth = depthFinal;
    return o;
}
