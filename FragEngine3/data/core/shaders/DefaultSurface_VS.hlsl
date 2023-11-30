/****************** CONSTANTS: *****************/

cbuffer Global : register(b0)
{
	// Camera parameters:
    uint resolutionX;           // Render target width, in pixels.
    uint resolutionY;           // Render target height, in pixels.
    float nearClipPlane;        // Camera's near clipping plane distance.
    float farClipPlane;         // Camera's far clipping plane distance.

    // Camera vectors & matrices:
    float3 cameraPosition;      // Camera position, in world space.
    float3 cameraDirection;     // Camera forward facing direction, in world space.
    float4x4 mtxCamera;         // Camera's full projection matrix, transforming from world space to viewport pixel coordinates.

    // Lighting:
    uint lightCount;
};

cbuffer Object : register(b1)
{
    float4x4 mtxInvWorld;       // Inverse world matrix, transforming vertices from model space to world space.
};

/**************** VERTEX INPUT: ****************/

struct VertexInput_Basic
{
    float3 position : POSITION;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

struct VertexInput_Extended
{
    float3 tangent : NORMAL1;
    float2 uv2 : TEXCOORD1;
};

struct VertexInput_BlendShapes
{
    uint4 blendIndices : NORMAL2;
    float4 blendWeights : TEXCOORD2;
};

struct VertexInput_BoneWeights
{
    uint4 blendIndices : NORMAL3;
    float4 blendWeights : TEXCOORD3;
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_Position;
    float3 worldPosition : POSITION;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

struct VertexOutput_Extended
{
    float3 tangent : NORMAL1;
    float3 binormal : NORMAL2;
    float2 uv2 : TEXCOORD1;
};

/******************* SHADERS: ******************/

void Main_Vertex(in VertexInput_Basic inputBasic, out VertexOutput_Basic outputBasic)
{
    float4x4 mtxModel2Camera = mul(mtxCamera, mtxInvWorld);

    outputBasic.position = mul(mtxModel2Camera, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxInvWorld, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = mul(mtxInvWorld, float4(inputBasic.position, 0)).xyz;
    outputBasic.uv = inputBasic.uv;
}

void Main_Vertex_Ext(in VertexInput_Basic inputBasic, in VertexInput_Extended inputExt, out VertexOutput_Basic outputBasic, out VertexOutput_Extended outputExt)
{
    float4x4 mtxModel2Camera = mul(mtxCamera, mtxInvWorld);

    outputBasic.position = mul(mtxModel2Camera, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxInvWorld, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = mul(mtxModel2Camera, float4(inputBasic.position, 0)).xyz;
    outputBasic.uv = inputBasic.uv;

    outputExt.tangent = mul(mtxInvWorld, float4(inputExt.tangent, 0)).xyz;
    outputExt.binormal = cross(outputBasic.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
}
