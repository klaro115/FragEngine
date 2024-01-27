#pragma pack_matrix( column_major )

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

void Main_Vertex(
    in VertexInput_Basic inputBasic,
    out VertexOutput_Basic outputBasic)
{
    outputBasic.position = float4(inputBasic.position, 1);
    outputBasic.worldPosition = inputBasic.position;
    outputBasic.normal = inputBasic.normal;
    outputBasic.uv = inputBasic.uv;
}

void Main_Vertex_Ext(
    in VertexInput_Basic inputBasic,
    in VertexInput_Extended inputExt,
    out VertexOutput_Basic outputBasic,
    out VertexOutput_Extended outputExt)
{
    outputBasic.position = float4(inputBasic.position, 1);
    outputBasic.worldPosition = inputBasic.position;
    outputBasic.normal = inputBasic.normal;
    outputBasic.uv = inputBasic.uv;

    outputExt.tangent = inputExt.tangent;
    outputExt.binormal = cross(outputBasic.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
}
