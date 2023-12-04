/**************** VERTEX INPUT: ****************/

struct VertexInput_Basic
{
    float3 position : POSITION;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

/**************** VERTEX OUTPUT: ***************/

struct VertexOutput_Basic
{
    float4 position : SV_Position;
    float3 worldPosition : POSITION;
    float3 normal : NORMAL0;
    float2 uv : TEXCOORD0;
};

/******************* SHADERS: ******************/

void Main_Vertex(in VertexInput_Basic inputBasic, out VertexOutput_Basic outputBasic)
{
    outputBasic.position = float4(inputBasic.position, 1);
    outputBasic.worldPosition = inputBasic.position;
    outputBasic.normal = inputBasic.normal;
    outputBasic.uv = inputBasic.uv;
}
