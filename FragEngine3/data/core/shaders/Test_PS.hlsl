#pragma pack_matrix( column_major )

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

/******************* SHADERS: ******************/

static const float PI = 3.141592653;

float4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    //return float4(inputBasic.normal * 0.5 + float3(0.5, 0.5, 0.5), 1);
    //return float4(abs(inputBasic.uv), inputBasic.position.z, 1);
    const int2 a = sign(sin((inputBasic.uv + float2(10, 10)) * 40 / PI));
    const float b = max(a.x * a.y, 0.5);
    const float3 c = inputBasic.normal * 0.5 + float3(0.5, 0.5, 0.5);
    return float4(b * c, 1);
}
