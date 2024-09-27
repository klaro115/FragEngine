#pragma pack_matrix( column_major )

/******************* DEFINES: ******************/

// Variants:
#define VARIANT_EXTENDED            // Whether to always create a shader variant using extended surface data

/****************** CONSTANTS: *****************/

// Constant buffer containing all settings that apply for everything drawn by currently active camera:
cbuffer CBCamera : register(b1)
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

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
};

// Constant buffer containing heightmap settings:
cbuffer CBHeightmap : register(b4)
{
    float4 heightmapTiling;         // Offset (XY) and scale (ZW) of the heightmap. This can be used to crop a section of the height map.
    float minAltitude;              // Minimum displacement along surface normal, all vertices are moved by at least this much.
    float maxAltitude;              // Maximum displacement along surface normal, no vertex will move further than this.
    float altitudeRange;            // Difference between minimum and maximum displacement altitudes.
};

/***************** RESOURCES: ******************/

Texture2D<float> TexHeightmap : register(vs, t7);
SamplerState SamplerHeightmap : register(vs, s2);

/**************** VERTEX INPUT: ****************/

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
#endif //VARIANT_EXTENDED

/**************** VERTEX OUTPUT: ***************/

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

/****************** FUNCTIONS: *****************/

float GetHeightmapDisplacement(const float2 _inputUv)
{
    const float2 heightmapUv = (_inputUv + heightmapTiling.xy) * heightmapTiling.zw;
    const float heightFactor = TexHeightmap.SampleLevel(SamplerHeightmap, heightmapUv, 0);
    return minAltitude + heightFactor * altitudeRange;
}

void ApplyHeightmap(inout VertexInput_Basic _inputBasic)
{
    const float displacement = GetHeightmapDisplacement(_inputBasic.uv);

    _inputBasic.position += _inputBasic.normal * displacement;
}

/******************* SHADERS: ******************/

void Main_Vertex(
    in VertexInput_Basic inputBasic,
    out VertexOutput_Basic outputBasic)
{
    float4x4 mtxLocal2Clip = mul(mtxWorld2Clip, mtxLocal2World);
    float3 viewDir = worldPosition - cameraPosition.xyz;

    ApplyHeightmap(inputBasic);

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

    ApplyHeightmap(inputBasic);

    outputBasic.position = mul(mtxLocal2Clip, float4(inputBasic.position, 1));
    outputBasic.worldPosition = mul(mtxLocal2World, float4(inputBasic.position, 1)).xyz;
    outputBasic.normal = normalize(mul(mtxLocal2World, float4(inputBasic.normal, 0)).xyz);
    outputBasic.uv = inputBasic.uv;

    outputExt.tangent = normalize(mul(mtxLocal2World, float4(inputExt.tangent, 0)).xyz);
    outputExt.binormal = cross(outputBasic.normal, outputExt.tangent);
    outputExt.uv2 = inputExt.uv2;
}
#endif //VARIANT_EXTENDED
