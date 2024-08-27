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

half fragment Main_Pixel(
    VertexOutput_Basic inputBasic   [[ stage_in ]])
{
    return 1;
};

half fragment Main_Pixel_Ext(
    VertexOutput_Extended inputExt   [[ stage_in ]])
{
    return 1;
};
