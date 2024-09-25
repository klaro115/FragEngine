<h1>Shader Resource Binding:</h1>

**SLIGHTLY OUT-DATED! The Metal bindings and layouts are updated as specification, but do not reflect the current state of the implementation. At this time, the Metal rendering pipeline is not fully implemented and may not run without crashes.**

This guide provides a general overview of what vertex buffer data and which resources are available to shaders at run-time. These resources are provided by the standard graphics stack and are targeting standard shaders. If your game's shading requires different or additional resources, you may assign them yourself via [user-bound resources](#user-bound-resources).
For more information about the engine's lighting system and standard shaders, have a look at the [Lighting & Shading Guide](./Lighting&Shading%20Guide.md).
<br>

- [Shader Formats](#shader-formats)
- [Vertex Buffers](#vertex-buffers)
- [System-bound Constants \& Lights](#system-bound-constants--lights)
- [User-bound Resources](#user-bound-resources)

<br>


## Shader Formats
Shader import and compilation is supported for HLSL (Direct3D) and MSL (Metal) shading languages. HLSL is intended as the primary development language for shaders, since it is comparibly less troublesome than some of the other languages, but also because the _Direct3D_ shader compiler supports compilation to SPIR-V. This makes HLSL the current go-to source code language for both _Direct3D_ and _Vulkan_ graphics APIs.

The Fragment engine has its own dedicated file format for shader assets, called FSHA.
This formats allows the bundling of pre-compiled shader programs and source code. All compiled variants and source code blobs are labelled for their respective APIs and feature sets, allowing for selection and compilaton of shader variants on-demand and at run-time.
The full specification for the format can be found here: [FSHA Format Spec](../Formats/FSHA%20Format%20Spec.md)

FSHA should be the standard shader asset format used by the final release build of your app. All other source code formats rely entirely on run-time compilation of shaders, whilst FSHA is platform-spanning and supports pre-compiled shaders.

<br>


## Vertex Buffers
_Source code:_ [BasicVertex, ExtendedVertex, ...](../../FragEngine3/Graphics/Resources/VertexDataTypes.cs)

The engine expects between 1 and 4 vertex buffers for each piece of scene geometry. The first vertex buffer (`VertexInput_Basic`) contains positions, normals, and texture coordinates for each vertex of surface. The other 3 vertex buffers are optional, and contain additional geometry data or animation data for the those same vertices.

Basic vertex data must always be bound to the first vertex buffer slot, as it provides the minimum information that is needed to render a mesh's surface. If any of the other vertex buffers are available, they must be bound in the order listed in the table below.

The engine's graphics device is created using Veldrid's `ResourceBindingModel.Default` flag. On MacOS, this causes all vertex buffers to be bound to the very first buffer slots (`buffer(0..4)`).

| **Order** | **HLSL Struct**           | **Status** | **C\# Type**            | **MSL Index** |
| --------- | ------------------------- | ---------- | ----------------------- | ------------- |
|    1st    | `VertexInput_Basic`       |  required  | `BasicVertex`           |  buffer(0)    |
|    2nd    | `VertexInput_Extended`    |  optional  | `ExtendedVertex`        |  buffer(1)    |
|    3rd    | `VertexInput_BlendShapes` |  optional  | `IndexedWeightedVertex` |  buffer(2)    |
|    4th    | `VertexInput_BoneWeights` |  optional  | `IndexedWeightedVertex` |  buffer(3)    |

<br>


## System-bound Constants & Lights
_Source code:_ [CBScene](../../FragEngine3/Graphics/Components/ConstantBuffers/CBScene.cs), [CBCamera](../../FragEngine3/Graphics/Components/ConstantBuffers/CBCamera.cs), [CBObject](../../FragEngine3/Graphics/Components/ConstantBuffers/CBObject.cs)

The engine's graphics system defines 3 constant buffers that are updated and bound automatically for all renderers that draw scene geometry. A scene-wide constant buffer (`CBScene`) is owned by the graphics stack, and updated when a new frame begins. A second constant buffer (`CBCamera`) contains per-camera data. Each scene renderer component (ex.: `StaticMeshRenderer`) will own an instance of a constant buffer with object data (`CBObject`) that is updated on-demand right before a new draw call is issued.

After these 3 constant buffers comes a read-only structured buffer with light data (`BufLights`), which is populated for each camera by the graphics stack. This buffer contains light color, intensity, and directional information for each light source that may illuminate the contents of a camera's viewport.

On Vulkan and Direct3D, the light buffer buffer is always bound to the first texture register. On MacOS/Metal, it is bound as a buffer right after the constant buffers.

| **HLSL Resource**     | **Type**                     | **C\# Type**              | **HLSL Register** | **MSL Index** | **Owner**       |
| --------------------- | ---------------------------- | ------------------------- | ----------------- | ------------- | --------------- |
| `CBScene`             | Constant Buffer              | `CBScene`                 | b0                | buffer(+1)    | Graphics Stack  |
| `CBCamera`            | Constant Buffer              | `CBCamera`                | b1                | buffer(+2)    | Camera          |
| `CBObject`            | Constant Buffer              | `CBObject`                | b2                | buffer(+3)    | Renderer        |
| `CBDefaultSurface`    | Constant Buffer (optional)   | `CBDefaultSurface`        | b3                | buffer(+4)    | Material        |
| `BufLights`           | Structured Buffer (readonly) | `LightSourceData`         | t0                | buffer(+5)    | LightDataBuffer |
| `TexShadowMaps`       | Texture2DArray               | `Texture` (depth)         | t1                | texture(0)    | ShadowMapArray  |
| `TexShadowNormalMaps` | Texture2DArray               | `Texture` (RGBA32_Unorm)  | t2                | texture(0)    | ShadowMapArray  |
| `BufShadowMatrices`   | Structured Buffer (readonly) | `Matrix4x4[]`<sup>1</sup> | t3                | buffer(+6)    | ShadowMapArray  |
| `SamplerShadowMaps`   | Sampler                      | `Sampler`                 | s0                | sampler(0)    | ShadowMapArray  |

<sup>1</sup> _The shadow matrix buffer contains more matrices than there are shadow-casting lights in the scene. For each light source that generates shadow maps, 2 matrices are created per shadow cascade. The first matrix is the shadow projection matrix, the second one is its inverse.
The struct `LightSourceData` details the number and starting offset of its set of shadow maps and matrices, along with the number of cascades the light has._

<br>


## User-bound Resources

Any additional buffers may be bound by user code or the graphics stack after the above buffers' slots.

| **HLSL Resource** | **Type**                     |  **C\# Type** | **HLSL Register** | **MSL Index** | **Owner**                 |
| ----------------- | ---------------------------- | ------------- | ----------------- | ------------- | ------------------------- |
| `[CbCustom]`      | Constant Buffer              | `<T>`         | b4+               | buffer(7+)    | Material<sup>2</sup>      |
| `[BufCustom]`     | Structured Buffer (readonly) | `<T>`         | t4+               | buffer(7+)    | Material<sup>2</sup>      |
| `[TexCustom]`     | Texture (any)                | `Texture`     | t4+               | texture(1+)   | Material<sup>2</sup>      |
| `[SamplerCustom]` | Sampler                      | `Sampler`     | s1+               | sampler(1+)   | Graphics Core<sup>3</sup> |

<sup>2</sup> _Custom textures and buffers are managed and assigned by the material, but the actual ownership of the resource objects remains with ResourceManager._

<sup>3</sup> _Samplers are managed and loaded on-demand by the material, but the actual ownership of all `Sampler` objects lies with the graphics core's `SamplerManager`. Since samplers do not hold any material-specific data, they may be re-used across the application, to ensure only one instance of each type of sampler exists at a time._

