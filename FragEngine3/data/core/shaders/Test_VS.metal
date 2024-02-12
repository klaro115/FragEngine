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

struct VertexInput_Extended
{
    float3 tangent          [[ attribute( 3 ) ]];
    float2 uv2              [[ attribute( 4 ) ]];
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];
};

struct VertexOutput_Extended
{
    float4 position         [[ position ]];
    float3 worldPosition    [[ user(worldPosition) ]];
    float3 normal           [[ user(normal) ]];
    float2 uv               [[ user(uv) ]];

    float3 tangent          [[ user(tangent) ]];
    float3 binormal         [[ user(binormal) ]];
    float2 uv2              [[ user(uv2) ]];
};

/******************* SHADERS: ******************/

VertexOutput_Basic vertex Main_Vertex(
    device const VertexInput_Basic* pInputBasic     [[ buffer( 0 ) ]],
    uint vertexId                                   [[ vertex_id ]])
{
    const device VertexInput_Basic& inputBasic = pInputBasic[vertexId];

    VertexOutput_Basic outputBasic;
    outputBasic.position = float4(inputBasic.position, 1);
    outputBasic.worldPosition = inputBasic.position;
    outputBasic.normal = inputBasic.normal;
    outputBasic.uv = inputBasic.uv;

    return outputBasic;
}

VertexOutput_Extended vertex Main_Vertex_Ext(
    device const VertexInput_Basic* pInputBasic     [[ buffer( 0 ) ]],
    device const VertexInput_Extended* pInputExt    [[ buffer( 1 ) ]],
    uint vertexId                                   [[ vertex_id ]])
{
    const device VertexInput_Basic& inputBasic = pInputBasic[vertexId];
    const device VertexInput_Extended& inputExt = pInputExt[vertexId];

    VertexOutput_Extended outputExt;
    outputExt.position = float4(inputBasic.position, 1);
    outputExt.worldPosition = inputBasic.position;
    outputExt.normal = inputBasic.normal;
    outputExt.uv = inputBasic.uv;

    outputExt.tangent = inputExt.tangent;
    outputExt.binormal = cross(outputExt.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
    
    return outputExt;
}
