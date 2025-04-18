/******************* DEFINES: ******************/
//<DEF>

#pragma pack_matrix( column_major )

// Variants:
#define VARIANT_EXTENDED                        // Whether to always create a shader variant using extended surface data
#define VARIANT_BLENDSHAPES                     // Whether to always create a shader variant using blend shape data
#define VARIANT_ANIMATED                        // Whether to always create a shader variant using bone animation data

//</DEF>
/****************** INCLUDES: ******************/
//<INC>

#include "../includes/VertexData/VertexInput.hlsl"
#include "../includes/VertexData/VertexOutput.hlsl"
#include "../includes/ConstantBuffers/CBCamera.hlsl"
#include "../includes/ConstantBuffers/CBObject.hlsl"

#ifdef VARIANT_ANIMATED
#include "../includes/BoneAnimation.hlsl"
#endif

//</INC>
/******************* SHADERS: ******************/
//<FNC>

void Main_Vertex(
    in VertexInput_Basic inputBasic,
    out VertexOutput_Basic outputBasic)
{
    float4x4 mtxLocal2Clip = mul(mtxWorld2Clip, mtxLocal2World);
    float3 viewDir = worldPosition - cameraPosition.xyz;

    outputBasic.position = mul(mtxLocal2Clip, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxLocal2World, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = normalize(mul(mtxLocal2World, float4(inputBasic.normal, 0)).xyz);
    outputBasic.uv = inputBasic.uv;
}

#ifdef VARIANT_EXTENDED
void Main_Vertex_Ext(
    in VertexInput_Basic inputBasic,
    in VertexInput_Extended inputExt,
    out VertexOutput_Basic outputBasic,
    out VertexOutput_Extended outputExt)
{
    float4x4 mtxLocal2Clip = mul(mtxWorld2Clip, mtxLocal2World);
    float3 viewDir = worldPosition - cameraPosition.xyz;

    outputBasic.position = mul(mtxLocal2Clip, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxLocal2World, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = normalize(mul(mtxLocal2World, float4(inputBasic.normal, 0)).xyz);
    outputBasic.uv = inputBasic.uv;

    outputExt.tangent = normalize(mul(mtxLocal2World, float4(inputExt.tangent, 0)).xyz);
    outputExt.binormal = cross(outputBasic.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
}
#endif //VARIANT_EXTENDED

// ANIMATED VARIANTS:

#ifdef VARIANT_ANIMATED
void Main_Vertex_Anim(
    in VertexInput_Basic inputBasic,
    in VertexInput_BoneWeights inputAnim,
    out VertexOutput_Basic outputBasic)
{
    float4x4 mtxLocal2Clip = mul(mtxWorld2Clip, mtxLocal2World);
    float3 viewDir = worldPosition - cameraPosition.xyz;

    ApplyBoneAnimation(inputBasic, inputAnim);

    outputBasic.position = mul(mtxLocal2Clip, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxLocal2World, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = normalize(mul(mtxLocal2World, float4(inputBasic.normal, 0)).xyz);
    outputBasic.uv = inputBasic.uv;
}
#endif //VARIANT_ANIMATED

#if defined(VARIANT_EXTENDED) && defined(VARIANT_ANIMATED)
void Main_Vertex_Ext_Anim(
    in VertexInput_Basic inputBasic,
    in VertexInput_Extended inputExt,
    in VertexInput_BoneWeights inputAnim,
    out VertexOutput_Basic outputBasic,
    out VertexOutput_Extended outputExt)
{
    float4x4 mtxLocal2Clip = mul(mtxWorld2Clip, mtxLocal2World);
    float3 viewDir = worldPosition - cameraPosition.xyz;

    ApplyBoneAnimation_Ext(inputBasic, inputExt, inputAnim);

    outputBasic.position = mul(mtxLocal2Clip, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxLocal2World, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = normalize(mul(mtxLocal2World, float4(inputBasic.normal, 0)).xyz);
    outputBasic.uv = inputBasic.uv;

    outputExt.tangent = normalize(mul(mtxLocal2World, float4(inputExt.tangent, 0)).xyz);
    outputExt.binormal = cross(outputBasic.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
}
#endif //VARIANT_EXTENDED && VARIANT_ANIMATED

//</FNC>
