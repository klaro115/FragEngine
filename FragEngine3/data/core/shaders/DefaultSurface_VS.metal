#include <metal_stdlib>
using namespace metal;

/****************** CONSTANTS: *****************/

// Constant buffer containing all settings that apply for everything drawn by currently active camera:
struct CBCamera
{
    // Camera vectors & matrices:
    float4x4 mtxWorld2Clip;         // Camera's full projection matrix, transforming from world space to clip space coordinates.
    float4 cameraPosition;          // Camera position, in world space.
    float4 cameraDirection;         // Camera forward facing direction, in world space.
    float4x4 mtxCameraMotion;       // Camera movement matrix, encoding motion/transformation from previous to current frame.

	// Camera parameters:
    uint cameraIdx;                 // Index of the currently drawing camera.
    uint resolutionX;               // Render target width, in pixels.
    uint resolutionY;               // Render target height, in pixels.
    float nearClipPlane;            // Camera's near clipping plane distance.
    float farClipPlane;             // Camera's far clipping plane distance.

    // Per-camera lighting:
    uint lightCount;                // Total number of lights affecting this camera.
    uint shadowMappedLightCount;    // Total number of lights that have a layer of the shadow map texture array assigned.
};

struct CBObject
{
    float4x4 mtxLocal2World;    // Object world matrix, transforming vertices from model space to world space.
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
    device const VertexInput_Basic* pInputBasic [[ buffer( 0 ) ]],
    device const CBCamera& cbCamera             [[ buffer( 1 ) ]],
    device const CBObject& cbObject             [[ buffer( 2 ) ]],
    uint vertexId                               [[ vertex_id ]])
{
    //^NOTE: Using 'ResourceBindingModel.Default', vertex buffers come before actual resources.
    // See this page for full explanation: https://veldrid.dev/articles/shaders.html

    const device VertexInput_Basic& inputBasic = pInputBasic[vertexId];

    float4x4 mtxLocal2Clip = cbCamera.mtxWorld2Clip * cbObject.mtxLocal2World;

    float4 projResult = mtxLocal2Clip * float4(inputBasic.position, 1);

    VertexOutput_Basic outputBasic;
    outputBasic.position = projResult;
    outputBasic.worldPosition = (cbObject.mtxLocal2World * float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = normalize((cbObject.mtxLocal2World * float4(inputBasic.normal, 0)).xyz);
    outputBasic.uv = inputBasic.uv;
    return outputBasic;
}

VertexOutput_Extended vertex Main_Vertex_Ext(
    device const VertexInput_Basic* pInputBasic     [[ buffer( 0 ) ]],
    device const VertexInput_Extended* pInputExt    [[ buffer( 1 ) ]],
    device const CBCamera& cbCamera                 [[ buffer( 2 ) ]],
    device const CBObject& cbObject                 [[ buffer( 3 ) ]],
    uint vertexId                                   [[ vertex_id ]])
{
    const device VertexInput_Basic& inputBasic = pInputBasic[vertexId];
    const device VertexInput_Extended& inputExt = pInputExt[vertexId];

    float4x4 mtxLocal2Clip = cbCamera.mtxWorld2Clip * cbObject.mtxLocal2World;

    float4 projResult = mtxLocal2Clip * float4(inputBasic.position, 1);

    VertexOutput_Extended outputExt;
    outputExt.position = projResult;
    outputExt.worldPosition = (cbObject.mtxLocal2World * float4(inputBasic.position, 1)).xyz;
    outputExt.normal = normalize((cbObject.mtxLocal2World * float4(inputBasic.normal, 0)).xyz);
    outputExt.uv = inputBasic.uv;

    outputExt.tangent = normalize((cbObject.mtxLocal2World * float4(inputExt.tangent, 0)).xyz);
    outputExt.binormal = cross(outputExt.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
    return outputExt;
}
