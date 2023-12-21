//#pragma pack_matrix( column_major )
#include <metal_stdlib>
using namespace metal;

/**************** VERTEX INPUT: ****************/

struct VertexInput_Basic
{
    float3 position         [[ attribute( 0 ) ]];
    float3 normal           [[ attribute( 1 ) ]];
    float2 uv               [[ attribute( 2 ) ]];
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition;
    float3 normal;
    float2 uv;
};

/******************* SHADERS: ******************/

VertexOutput_Basic vertex Main_Vertex(
    device const VertexInput_Basic* pInputBasic [[ buffer( 0 ) ]],
    uint vertexId                               [[ vertex_id ]])
{
    const device VertexInput_Basic& inputBasic = pInputBasic[vertexId];

    VertexOutput_Basic outputBasic;
    outputBasic.position = float4(inputBasic.position, 1);
    outputBasic.worldPosition = inputBasic.position;
    outputBasic.normal = inputBasic.normal;
    outputBasic.uv = inputBasic.uv;
    return outputBasic;
}
