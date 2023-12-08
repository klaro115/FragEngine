/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

/******************* SHADERS: ******************/

float4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    return float4(abs(inputBasic.uv), inputBasic.position.z, 1);
}
