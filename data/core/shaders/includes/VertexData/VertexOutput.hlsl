#ifndef __HAS_VERTEX_OUTPUT__
#define __HAS_VERTEX_OUTPUT__

/****************** RESOURCES: *****************/
//<RES>

struct VertexOutput_Basic
{
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

#ifdef VARIANT_EXTENDED
struct VertexOutput_Extended
{
    float3 tangent : TANGENT0;
    float3 binormal : NORMAL1;
    float2 uv2 : TEXCOORD1;
};
#endif //VARIANT_EXTENDED

//</RES>
#endif //__HAS_VERTEX_OUTPUT__
