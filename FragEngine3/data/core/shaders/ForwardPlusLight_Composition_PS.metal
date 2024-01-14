//#pragma pack_matrix( column_major )
#include <metal_stdlib>
using namespace metal;

/****************** CONSTANTS: *****************/

struct CBGlobal
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
    //float3 ambientLight;
    float4 ambientLightLow;
    float4 ambientLightMid;
    float4 ambientLightHigh;
    uint lightCount;
    float shadowFadeStart;      // Percentage of the shadow distance in projection space where they start fading out.
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition;
    float3 normal;
    float2 uv;
};

/***************** PIXEL OUTPUT: ***************/

//struct PixelOutput
//{
//    half4 color : SV_Target0;
//    //float depth : SV_Depth;
//};

/******************* SHADERS: ******************/

half4 fragment Main_Pixel(
    VertexOutput_Basic inputBasic                       [[ stage_in ]],
    device const CBGlobal& cbGlobal                     [[ buffer( 1 ) ]],
    texture2d<half, access::read> TexOpaqueColor        [[ texture( 1 ) ]],     // texture slot 0 occupied by shadow maps
    texture2d<float, access::read> TexOpaqueDepth       [[ texture( 2 ) ]],
    texture2d<half, access::read> TexTransparentColor   [[ texture( 3 ) ]],
    texture2d<float, access::read> TexTransparentDepth  [[ texture( 4 ) ]],
    texture2d<half, access::read> TexUIColor            [[ texture( 5 ) ]])
{
    // Determine source pixel location from fullscreen quad's UV:
    uint2 posPixel = (uint2)(inputBasic.uv * float2(cbGlobal.resolutionX, cbGlobal.resolutionY));

    // Load pixel color and depth for all textures:
    half4 colOpaque = TexOpaqueColor.read(posPixel);
    float depthOpaque = TexOpaqueDepth.read(posPixel).r;

    half4 colTransparent = TexTransparentColor.read(posPixel);
    float depthTransparent = TexTransparentDepth.read(posPixel).r;

    half4 colUI = TexUIColor.read(posPixel);

    // Composite geometry: (opaque & transparent)
    half k = depthTransparent > depthOpaque ? colTransparent.w : 0;
    half4 colGeometry = mix(colOpaque, colTransparent, k);
    float depthGeometry = min(depthTransparent, depthOpaque);

    // Overlay UI:
    half alphaFinal = clamp(colGeometry.w + colUI.w, (half)0, (half)1);
    half4 colFinal = half4(mix(colGeometry.xyz, colUI.xyz, colUI.w), alphaFinal);
    float depthFinal = colUI.w <= 0.001 ? depthGeometry : 0;
    
    // Assemble final output:
    return colFinal;
}
