//#pragma pack_matrix( column_major )
#include <metal_stdlib>
using namespace metal;

/****************** CONSTANTS: *****************/

struct CBGlobal
{
    // Camera vectors & matrices:
    float4x4 mtxCamera;         // Camera's full projection matrix, transforming from world space to clip space coordinates.
    float4 cameraPosition;      // Camera position, in world space.
    float4 cameraDirection;     // Camera forward facing direction, in world space.

	// Camera parameters:
    uint resolutionX;           // Render target width, in pixels.
    uint resolutionY;           // Render target height, in pixels.
    float nearClipPlane;        // Camera's near clipping plane distance.
    float farClipPlane;         // Camera's far clipping plane distance.

    // Lighting:
    float3 ambientLight;
    uint lightCount;
};

struct CBObject
{
    float4x4 mtxWorld;          // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;       // World space position of the object.
    float boundingRadius;       // Bounding sphere radius of the object.
};

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

struct VertexInput_BlendShapes
{
    uint4 blendIndices      [[ attribute( 5 ) ]];
    float4 blendWeights     [[ attribute( 6 ) ]];
};

struct VertexInput_BoneWeights
{
    uint4 blendIndices      [[ attribute( 7 ) ]];
    float4 blendWeights     [[ attribute( 8 ) ]];
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position         [[ position ]];
    float3 worldPosition;
    float3 normal;
    float2 uv;
};

struct VertexOutput_Extended
{
    float3 tangent;
    float3 binormal;
    float2 uv2;
};

/******************* SHADERS: ******************/

VertexOutput_Basic vertex Main_Vertex(
    device const CBGlobal& cbGlobal             [[ buffer( 0 ) ]],
    device const CBObject& cbObject             [[ buffer( 1 ) ]],
    device const VertexInput_Basic* pInputBasic [[ buffer( 2 ) ]],
    uint vertexId                               [[ vertex_id ]])
{
    //^NOTE: Using 'ResourceBindingModel.Improved', vertex buffers come after actual resources.
    // See this page for full explanation: https://veldrid.dev/articles/shaders.html

    const device VertexInput_Basic& inputBasic = pInputBasic[vertexId];

    float4x4 mtxModel2Camera = cbGlobal.mtxCamera * cbObject.mtxWorld;

    float4 projResult = mtxModel2Camera * float4(inputBasic.position, 1);

    VertexOutput_Basic outputBasic;
    outputBasic.position = projResult;
    outputBasic.worldPosition = (cbObject.mtxWorld * float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = (cbObject.mtxWorld * float4(inputBasic.position, 0)).xyz;
    outputBasic.uv = inputBasic.uv;
    return outputBasic;
}
