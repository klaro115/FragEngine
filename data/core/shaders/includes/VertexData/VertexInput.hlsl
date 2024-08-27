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
// ^Note: Per-index blend shape offsets are provided as a separate vertex buffer, of element type float3.
// This will be a single contiguous array of all position offsets across all vertices and blend groups.
// The blend shape offsets vertex buffer is bound after all per-vertex/surface vertex buffers.
#endif

#ifdef VARIANT_ANIMATION
struct VertexInput_BoneWeights
{
	uint4 indices : BLENDINDICES1;
	float4 weights : BLENDWEIGHT1;
};
// ^Note: Per-bone transformation matrices are provided as a separate vertex buffer, of element type float4x4.
// This will be a single contiguous array of marices of all bones, in the root object's model space. The bone
// matrix vertex buffer is bound after all per-vertex/surface vertex buffers.
#endif

//</RES>
#endif //__HAS_VERTEX_INPUT__
