/******************* DEFINES: ******************/
//<DEF>

#pragma pack_matrix( column_major )

//</DEF>
/****************** INCLUDES: ******************/
//<INC>

#include "../includes/VertexData/VertexOutput.hlsl"
#include "../includes/ConstantBuffers/CBCamera.hlsl"

//</INC>
/***************** PIXEL OUTPUT: ***************/
//<RES>

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

//</RES>
/******************* SHADERS: ******************/
//<FNC>

PixelOutput Main_Pixel(in VertexOutput_Basic inputBasic)
{
    // Determine source pixel location from fullscreen quad's UV:
    const int3 posPixel = int3(inputBasic.uv.x * resolutionX, (1.0 - inputBasic.uv.y) * resolutionY, 0);

    // Load pixel color and depth for all textures:
    const half4 colOpaque = TexOpaqueColor.Load(posPixel);
    const float depthOpaque = TexOpaqueDepth.Load(posPixel).r;

    const half4 colTransparent = TexTransparentColor.Load(posPixel);
    const float depthTransparent = TexTransparentDepth.Load(posPixel).r;
    const bool isVisible = colTransparent.w > 0.003;

    // Composite geometry: (opaque & transparent)
    const half k = depthTransparent < depthOpaque && isVisible ? colTransparent.w : 0;
    const half4 colGeometry = lerp(colOpaque, colTransparent, k);
    const float depthGeometry = isVisible ? min(depthTransparent, depthOpaque) : depthOpaque;

    // Assemble final output:
    PixelOutput o;
    o.color = colGeometry;
    o.depth = depthGeometry;
    return o;
}

//</FNC>
