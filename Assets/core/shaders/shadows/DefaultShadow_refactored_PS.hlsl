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

#include "../includes/VertexData/VertexOutput.hlsl"

//</INC>
/******************* SHADERS: ******************/
//<FNC>

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    half3 normal = ((half3)inputBasic.normal + 1) * 0.5;
    return half4(normal, 1);
};

#ifdef VARIANT_EXTENDED
half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0
{
    half3 normal = ((half3)inputBasic.normal + 1) * 0.5;
    return half4(normal, 1);
};
#endif

//</FNC>
