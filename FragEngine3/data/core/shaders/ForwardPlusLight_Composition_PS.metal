#include <metal_stdlib>
using namespace metal;

/****************** CONSTANTS: *****************/

// Constant buffer containing all settings that apply for everything drawn by currently active camera:
struct CBCamera
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
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];
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
    device const CBCamera& cbCamera                     [[ buffer( 1 ) ]],
    texture2d<half, access::read> TexOpaqueColor        [[ texture( 1 ) ]],     // texture slot 0 occupied by shadow maps
    texture2d<float, access::read> TexOpaqueDepth       [[ texture( 2 ) ]],
    texture2d<half, access::read> TexTransparentColor   [[ texture( 3 ) ]],
    texture2d<float, access::read> TexTransparentDepth  [[ texture( 4 ) ]],
    texture2d<half, access::read> TexUIColor            [[ texture( 5 ) ]])
{
    // Determine source pixel location from fullscreen quad's UV:
    const uint2 posPixel = (uint2)(inputBasic.uv * float2(cbCamera.resolutionX, cbCamera.resolutionY));

    // Load pixel color and depth for all textures:
    const half4 colOpaque = TexOpaqueColor.read(posPixel);
    const float depthOpaque = TexOpaqueDepth.read(posPixel).r;

    const half4 colTransparent = TexTransparentColor.read(posPixel);
    const float depthTransparent = TexTransparentDepth.read(posPixel).r;

    const half4 colUI = TexUIColor.read(posPixel);

    // Composite geometry: (opaque & transparent)
    const half k = depthTransparent > depthOpaque ? colTransparent.w : 0;
    const half4 colGeometry = mix(colOpaque, colTransparent, k);
    const float depthGeometry = min(depthTransparent, depthOpaque);

    // Overlay UI:
    const half alphaFinal = clamp(colGeometry.w + colUI.w, (half)0, (half)1);
    half4 colFinal = half4(mix(colGeometry.xyz, colUI.xyz, colUI.w), alphaFinal);
    const float depthFinal = colUI.w <= 0.001 ? depthGeometry : 0;
    
    //if (inputBasic.uv.x < 0.5 && inputBasic.uv.y < 0.5)
    {
        colFinal = half4(abs((half2)inputBasic.position.xy / half2(cbCamera.resolutionX, cbCamera.resolutionY)), 0, 1);
        //colFinal.xy *= (half2)inputBasic.uv;
        //colFinal = half4(0.5 + 0.5 * colFinal.x, colFinal.yz * (half2)inputBasic.uv, 1);
    }

    // Assemble final output:
    return colFinal;
}
