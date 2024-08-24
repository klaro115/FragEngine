#ifndef __HAS_CB_DEFAULT_SURFACE__
#define __HAS_CB_DEFAULT_SURFACE__

//<RES>

// Constant buffer containing material-specific settings:
cbuffer CBDefaultSurface : register(b3)
{
    float4 tintColor;               // Color tint applied to albedo.
    float roughness;                // Roughness rating of the surface.
    float shininess;                // How shiny or metallic the surface is.
    float reflectionIndex;          // Reflection index of the material's surface.
    float refractionIndex;          // Refraction index of the material's substance.
};

//</RES>
#endif //__HAS_CB_DEFAULT_SURFACE__
