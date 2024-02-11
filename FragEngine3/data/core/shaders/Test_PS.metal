//#pragma pack_matrix( column_major )
#include <metal_stdlib>
using namespace metal;

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];
};

/******************* SHADERS: ******************/

half4 fragment Main_Pixel(
    VertexOutput_Basic inputBasic   [[ stage_in ]])
{
    half2 a = sign(sin(((half2)inputBasic.uv + half2(10, 10)) * 40 * M_1_PI_H));
    half b = max(a.x * a.y, (half)0.5);
    half3 c = (half3)inputBasic.normal * 0.5 + half3(0.5, 0.5, 0.5);
    return half4(b * c, 1);
}
