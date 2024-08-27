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

// Scene geometry:
Texture2D<half4> TexSceneColor : register(ps, t4);
Texture2D<float2> TexSceneDepth : register(ps, t5);

// UI Overlay:
Texture2D<half4> TexUIColor : register(ps, t6);

//</RES>
/******************* SHADERS: ******************/
//<FNC>

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

//</FNC>
