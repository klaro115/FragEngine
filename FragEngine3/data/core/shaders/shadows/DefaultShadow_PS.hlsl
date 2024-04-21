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

/******************* SHADERS: ******************/

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    half3 normal = ((half3)inputBasic.normal + 1) * 0.5;
    return half4(normal, 1);
};

half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0
{
    half3 normal = ((half3)inputBasic.normal + 1) * 0.5;
    return half4(normal, 1);
};
