//#pragma pack_matrix( column_major )

/******************* DEFINES: ******************/
//<DEF>

// Albedo:
#define FEATURE_ALBEDO_TEXTURE 1                // Whether to initialize albedo color from main texture. If false, a color literal is used
#define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)  // Color literal from which albedo may be initialized

// Normals:
#define FEATURE_NORMALS                         // Whether to use normal maps in all further shading
#define FEATURE_PARALLAX                        // Whether to use height/parallax maps to modulate UV sampling
#define FEATURE_PARALLAX_FULL                   // Whether to use full iteratively traced parallax with occlusion, instead of just simple UV offsetting.

// Lighting:
#define FEATURE_LIGHT                           // Whether to apply lighting
#define FEATURE_LIGHT_AMBIENT                   // Whether to add directional ambient intensity to base lighting
#define FEATURE_LIGHT_LIGHTMAPS                 // Whether to add light map intensity to base lighting
#define FEATURE_LIGHT_SOURCES                   // Whether to use light sources from the scene to light up the object
#define FEATURE_LIGHT_MODEL Phong
#define FEATURE_LIGHT_SHADOWMAPS                // Whether to use shadow maps to mask out light rays coming from light sources
#define FEATURE_LIGHT_SHADOWMAPS_RES 1024       // The resolution of shadow maps, in pixels per side.
#define FEATURE_LIGHT_SHADOWMAPS_AA 2           // If defined, controls the number of depth samples read from shadow map per pixel 
#define FEATURE_LIGHT_INDIRECT 5                // If defined, controls the number of indirect light samples per pixel, sample count is NxN, must be 2 or higher

// Variants:
#define VARIANT_EXTENDED                        // Whether to always create a shader variant using extended surface data
#define VARIANT_BLENDSHAPES                     // Whether to always create a shader variant using blend shape data
#define VARIANT_ANIMATED                        // Whether to always create a shader variant using bone animation data

#if FEATURE_ALBEDO_TEXTURE == 0
    #ifndef FEATURE_ALBEDO_COLOR
        #define FEATURE_ALBEDO_COLOR half4(1, 1, 1, 1)
    #endif
#endif

//^TODO: Instead of replacing on import, prepend these only before compilation?

//</DEF>
/****************** INCLUDES: ******************/
//<INC>

#include "./includes/VertexData/VertexOutput.hlsl"
#include "./includes/Normals.hlsl"
#include "./includes/Parallax.hlsl"
#include "./includes/Lighting/Lighting.hlsl"

//</INC>
/****************** RESOURCES: *****************/
//<RES>

#if FEATURE_ALBEDO_TEXTURE == 1
Texture2D<half4> TexMain : register(ps, t4);
#endif //FEATURE_ALBEDO_TEXTURE == 1

#if !defined(HAS_SAMPLER_MAIN) && FEATURE_ALBEDO_TEXTURE == 1
#define HAS_SAMPLER_MAIN
    SamplerState SamplerMain : register(s1);
#endif //HAS_SAMPLER_MAIN

//</RES>
/******************* SHADERS: ******************/
//<FNC>

half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0
{
    // Recalculate UV from parallax map:
	float2 uv = inputBasic.uv;
	ApplyParallaxMap(uv, inputBasic.worldPosition, inputBasic.normal);

    #if FEATURE_ALBEDO_TEXTURE == 1
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, uv);
    #else
    half4 albedo = FEATURE_ALBEDO_COLOR;
    #endif //FEATURE_ALBEDO_TEXTURE == 1

    // Calculate normals from normal map:
	half3 worldNormal = (half3)inputBasic.normal;
	ApplyNormalMap(worldNormal, half3(0, 0, 1), half3(1, 0, 0), uv);

    // Calculate lighting:
    ApplyLighting(albedo, inputBasic.worldPosition, worldNormal, inputBasic.normal, uv);

    // Return final color:
    return albedo;
};

#ifdef VARIANT_EXTENDED
half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0
{
    // Recalculate UV from parallax map:
	float2 uv = inputBasic.uv;
	ApplyParallaxMap(uv, inputBasic.worldPosition, inputBasic.normal);

    #if FEATURE_ALBEDO_TEXTURE == 1
    // Sample base color from main texture:
    half4 albedo = TexMain.Sample(SamplerMain, uv);
    #else
    half4 albedo = FEATURE_ALBEDO_COLOR;
    #endif //FEATURE_ALBEDO_TEXTURE == 1

    // Calculate normals from normal map:
	half3 worldNormal = (half3)inputBasic.normal;
	ApplyNormalMap(worldNormal, inputExt.tangent, inputExt.binormal, uv);

    // Calculate lighting:
    ApplyLighting(albedo, inputBasic.worldPosition, worldNormal, inputBasic.normal, uv);

    // Return final color:
    return albedo;
};
#endif //VARIANT_EXTENDED

//</FNC>
