#pragma pack_matrix( column_major )

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
    float3 tangent : TANGENT0;
    float3 binormal : NORMAL1;
    float2 uv2 : TEXCOORD1;
};

/****************** RESOURCES: *****************/

Texture2D<half4> TexMain : register(ps, t3);
SamplerState SamplerMain : register(s1);

/******************* SHADERS: ******************/

#define MIN_ALPHA 0.05

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    half depth = TexMain.Sample(SamplerMain, inputBasic.uv).w;
    if (depth < MIN_ALPHA)
    {
        discard;
    }
    half3 normal = ((half3)inputBasic.normal + 1) * 0.5;
    return half4(normal, depth);
};

half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0
{
    half depth = TexMain.Sample(SamplerMain, inputBasic.uv).w;
    if (depth < MIN_ALPHA)
    {
        discard;
    }
    half3 normal = ((half3)inputBasic.normal + 1) * 0.5;
    return half4(normal, depth);
};
