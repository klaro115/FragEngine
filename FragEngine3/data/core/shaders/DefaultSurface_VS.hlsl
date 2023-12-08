#pragma pack_matrix( row_major )

/****************** CONSTANTS: *****************/

cbuffer CBGlobal : register(b0)
{
    // Camera vectors & matrices:
    float4x4 mtxCamera;         // Camera's full projection matrix, transforming from world space to viewport pixel coordinates.
    float3 cameraPosition;      // Camera position, in world space.
    float3 cameraDirection;     // Camera forward facing direction, in world space.

	// Camera parameters:
    uint resolutionX;           // Render target width, in pixels.
    uint resolutionY;           // Render target height, in pixels.
    float nearClipPlane;        // Camera's near clipping plane distance.
    float farClipPlane;         // Camera's far clipping plane distance.

    // Lighting:
    uint lightCount;
};

cbuffer CBObject : register(b1)
{
    float4x4 mtxWorld;          // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;       // World space position of the object.
    float boundingRadius;       // Bounding sphere radius of the object.
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
    float4 position : SV_POSITION;
    float3 worldPosition : COLOR0;
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

static const float4x4 mtxIdentity = { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };

void Main_Vertex(in VertexInput_Basic inputBasic, out VertexOutput_Basic outputBasic)
{
    float4x4 mtxModel2Camera = mul(mtxCamera, mtxWorld);

    //float4 projResult = mul(mtxModel2Camera, float4(inputBasic.position, 1));

    outputBasic.position = mul(mtxCamera, float4(inputBasic.position, 1));
    //outputBasic.position = float4(inputBasic.position, 1);
    outputBasic.worldPosition = mul(mtxWorld, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = mul(mtxWorld, float4(inputBasic.position, 0)).xyz;
    outputBasic.uv = inputBasic.uv;
}

void Main_Vertex_Ext(in VertexInput_Basic inputBasic, in VertexInput_Extended inputExt, out VertexOutput_Basic outputBasic, out VertexOutput_Extended outputExt)
{
    float4x4 mtxModel2Camera = mul(mtxCamera, mtxWorld);

    float4 projResult = mul(mtxModel2Camera, float4(inputBasic.position, 1));

    outputBasic.position = projResult / projResult.w;
    outputBasic.worldPosition = mul(mtxWorld, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = mul(mtxModel2Camera, float4(inputBasic.position, 0)).xyz;
    outputBasic.uv = inputBasic.uv;

    outputExt.tangent = mul(mtxWorld, float4(inputExt.tangent, 0)).xyz;
    outputExt.binormal = cross(outputBasic.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
}
