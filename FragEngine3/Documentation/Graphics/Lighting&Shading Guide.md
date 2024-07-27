<h1>Lighting & Shading Guide</h1>

This page aims to provide a rough overview over the lighting system and shadingb model used by the engine. Other architectures are possible, though most of the graphics system is geared towards these standard shaders and lighting models.
If you're looking to write your own surface shaders instead, have a look at the [Shader Guide](./Shader%20Guide.md).
<br>

- [Architecture](#architecture)
- [Standard Shaders](#standard-shaders)
  - [ShaderGen Flags](#shadergen-flags)
- [Light \& Shadow](#light--shadow)

<br>


## Architecture
_Source code:_ [ForwardPlusLightsStack](../../FragEngine3/Graphics/Stack/ForwardPlusLightsStack.cs)

The engine's rendering and lighting system is primarily built for forward rendering plus lighting.
That is to say, shading of most surfaces is done using a single forward rendering pass during which all lights and shadows are applied as well.

The graphics pipeline and processing flow is governed almost entirely by the graphics stack. Each scene has a graphics stack attached to it, which schedules draw calls, rendering order, and output composition. By default, an instance of the `ForwardPlusLightsStack` will be attached to the scene, though developers are free to provide their own implementation to suit their graphical needs.

<br>


## Standard Shaders
_Source code:_ [DefaultSurface_modular_PS](../../data/core/shaders/DefaultSurface_modular_PS.hlsl), [Mtl_DefaultSurface](../../data/core/materials/Mtl_DefaultSurface.json)

Most of the core rendering pipeline is demonstrated by the [standard shader](../../data/core/shaders/DefaultSurface_modular_PS.hlsl), which offers a decent amount of feature customization through preprocessor macros. You may specify feature flags and constant through a descriptive string, when assigning the modular variant of the standard shader through a material.

For example, the material [Mtl_DefaultSurface](../../data/core/materials/Mtl_DefaultSurface.json) provides the pixel shader as `"Pixel": "ShaderGen='At_Nyn0_Ly101p100'_PS"`. Usually, you'd list a resource key here, but the `ShaderGen='...'` format notifies the importer that the pixel shader should instead be a procedurally generated variant of the standard shader. This variant is created at load-time by the engine's [shader generator](../../FragEngine3/Graphics/Resources/ShaderGen/ShaderGenerator.cs) system (i.e. ShaderGen), according to the parameter flags following the equal sign. Note that the standard shader's shader code is only read from file once when ShaderGen is first used, and the full modular shader code is then cached for all subsequent calls.


### ShaderGen Flags
_Source code:_ [ShaderGenConfig](../../FragEngine3/Graphics/Resources/ShaderGen/ShaderGenConfig.cs), [ShaderGenerator](../../FragEngine3/Graphics/Resources/ShaderGen/ShaderGenerator.cs)

The following provides a list of parameters and features that can be enabled through the ShaderGen string format. ShaderGen is supported in both vertex and pixel shaders and can be used by listing a feature string of the right format in the shader resource fields of a material's JSON. The shaders will automatically be loaded from the modular standard shaders, with features enabled or disabled according to the specification provided in the material.

The feature string is of the following format:
```
ShaderGen='At_Nyn0_Ly101p100'_PS
ShaderGen='Ac_Nyy1_Ly111p145_Al=6495edff_Ns=SamplerNormals'_PS
```
The format is split into different parts based on the purposes of their feature flags.
The second line shows an extreme case where all optional features are fully customized via suffixes to the format.
The "_PS" suffix in both cases tells the importer that this is a pixel shader; for vertex shaders, the suffix "_VS" should be used.

The following table lists all feature flags in order, how they are represented in HLSL code, and what resources they add to the shader:

| Feature               | HLSL Define                  | HLSL Values           | Feature Flags                        | Resources                      | Description                                     |
| --------------------- | ---------------------------- | --------------------- | ------------------------------------ | ------------------------------ | ----------------------------------------------- |
| Main texture          | FEATURE_ALBEDO_TEXTURE       | 0 or 1                | `At`, `As=[Sampler]`<sup>1</sup>     | `TexMain`,<br> `SamplerMain`   | Base albedo/color                               |
| Main color            | FEATURE_ALBEDO_COLOR         | `half4(R, G, B, A)`   | `Ac`, `Al=6495edff`                  | -                              | Base color/tint                                 |
| Normal map            | FEATURE_NORMALS              | -                     | `N[y/n]`, `Ns=[Sampler]`<sup>1</sup> | `TexNormal`,<br> `SamplerMain` | Surface normals                                 |
| Parallax map          | FEATURE_PARALLAX             | -                     | `Ny[y/n]`                            | `TexParallax`,<br> `SamplerMain`,<br> `CBCamera` | Parallax/Height map           |
| Quality parallax      | FEATURE_PARALLAX_FULL        | -                     | `Nyy[1/0]`                           | -                              | Slower but better parallax mapping              |
| Lighting              | FEATURE_LIGHT                | -                     | `L[y/n]`                             | -                              | Enables all lighting                            |
| Ambient lighting      | FEATURE_LIGHT_AMBIENT        | -                     | `Ly[1/0]`                            | `CBScene`                      | Scene's ambient lighting                        |
| Baked lightmap        | FEATURE_LIGHT_LIGHTMAPS      | -                     | `Ly0[1/0]`                           | `TexLightmap`,<br> `SamplerMain` | Lighting from a pre-baked light map           |
| Light sources         | FEATURE_LIGHT_SOURCES        | -                     | `Ly00[1/0]`                          | `BufLights`,<br> `CBCamera`    | Active lighting from light sources              |
| Lighting model        | FEATURE_LIGHT_MODEL          | `Phong`, `Cell`, etc. | `Ly001[p/c/...]`                     | -                              | Which lighting model to use, default is Phong   |
| Shadow maps           | FEATURE_LIGHT_SHADOWMAPS     | -                     | `Ly001p[1/0]`                        | `TexShadowMaps`,<br> `BufShadowMatrices`,<br> `SamplerShadowMaps` | Shadows projected using shadow/depth maps |
| Shadow map resolution | FEATURE_LIGHT_SHADOWMAPS_RES | 8-8192                | -                                    | `TexShadowMaps`                | Resolution of shadow maps<sup>2</sup>           |
| Shadow depth samples  | FEATURE_LIGHT_SHADOWMAPS_AA  | 2, 4, or 8            | `Ly001p1[1/0]`                       | `TexShadowMaps`,<br> `SamplerShadowMaps` | Number of shadow depth samples per pixel |
| Indirect lighting     | FEATURE_LIGHT_INDIRECT       | 2-10                  | `Ly001p10[1/0]`                      | `TexShadowNormalMaps`          | Indirect lighting approximated from shadow maps |
| Extended vertex data  | VARIANT_EXTENDED             | -                     | `V[1/0]`<sup>3</sup>                 | Vertex buffer 1                | Variant with extended vertex data               |
| Blend shapes          | VARIANT_BLENDSHAPES          | -                     | `V0[1/0]`<sup>3</sup>                | Vertex buffer 2                | Variant with blend shape data                   |
| Bone animation        | VARIANT_ANIMATED             | -                     | `V00[1/0]`<sup>3</sup>               | Vertex buffer 3                | Variant with bone animation weight data         |

<sup>1</sup> _Sampler name parameters are optional. If no sampler name is provided using `As` or `Ns` parameters, the standard texture sampler `SamplerMain` will be used._

<sup>2</sup> _Resolution of shadow maps is automatically set from a global [constant value](../../FragEngine3/Graphics/Lighting/LightConstants.cs). The appropriate preprocessor #define is updated in shader code, but the property does not have a feature flag._

<sup>3</sup> _Shader variants with [additional vertex data](../../FragEngine3/Graphics/Resources/VertexDataTypes.cs) will be generated if these flags are raised. The renderer will automatically use the variant with the largest vertex data set, depending on what vertex data is available. If no bone weights exist for a 3D model, the next more basic variant without bone animation support will be used for rendering that mesh._

<br>


## Light & Shadow

WIP
