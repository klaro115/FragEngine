#ifndef __HAS_CB_OBJECTS__
#define __HAS_CB_OBJECTS__

//<RES>

// Constant buffer containing only object-specific settings:
cbuffer CBObject : register(b2)
{
    float4x4 mtxLocal2World;        // Object world matrix, transforming vertices from model space to world space.
    float3 worldPosition;           // World space position of the object.
    float boundingRadius;           // Bounding sphere radius of the object.
};

//</RES>
#endif //__HAS_CB_OBJECTS__
