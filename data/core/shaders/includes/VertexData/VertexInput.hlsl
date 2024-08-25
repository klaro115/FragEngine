#ifndef __HAS_VERTEX_INPUT__
#define __HAS_VERTEX_INPUT__

/****************** RESOURCES: *****************/
//<RES>

struct VertexInput_Basic
{
    float3 position : POSITION;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

#ifdef VARIANT_EXTENDED
struct VertexInput_Extended
{
    float3 tangent : NORMAL1;
    float2 uv2 : TEXCOORD1;
};
#endif

#ifdef VARIANT_BLENDSHAPES
struct VertexInput_BlendShapes
{
	uint4 indices : BLENDINDICES0;
	float4 weights : BLENDWEIGHT0;
};
#endif

#ifdef VARIANT_ANIMATION
sruct VertexInput_Animation
{
	uint4 indices : BLENDINDICES1;
	float4 weights : BLENDWEIGHT1;
};
#endif

//</RES>
#endif //__HAS_VERTEX_INPUT__
