### Shader Buffer Order:

**OUT-DATED! Some of the following information no longer applies; major changes have been made, and this document needs to be updated once those changes are fully implemented.**

####Vertex Buffers:

The engine expects between 1 and 4 vertex buffers for each piece of scene geometry. The first vertex buffer (```VertexInput_Basic```) contains positions, normals, and texture coordinates for each vertex of surface. The other 3 vertex buffers are optional, and contain additional geometry data or animation data for the those same vertices.

Basic vertex data must always be bound to the first vertex buffer slot, as it provides the minimum information that is needed to render a mesh's surface. If any of the other vertex buffers are available, they must be bound in the order listed in the table below.

The engine's graphics device is created using Veldrid's ```ResourceBindingModel.Default``` flag. On MacOS, this causes all vertex buffers to be bound to the very first buffer slots (```buffer(0..4)```).

| **Order** | **HLSL Struct** | **Status** | **C\# Type** | **MSL Index** |
| --- | --- | --- | --- | --- |
| 1st | ```VertexInput_Basic``` | required | ```BasicVertex``` | buffer(0) |
| 2nd | ```VertexInput_Extended``` | optional | ```ExtendedVertex``` | buffer(1) |
| 3rd | ```VertexInput_BlendShapes``` | optional | ```IndexedWeightedVertex``` | buffer(2) |
| 4th | ```VertexInput_BoneWeights``` | optional | ```IndexedWeightedVertex``` | buffer(3) |


####System-bound Constants & Lights:

The engine's graphics system defines 2 constant buffers that are updated and bound automatically for all renderers that draw scene geometry. A global constant buffer (```CBGlobal```) is owned by each camera, and updated when a new frame begins. Each scene renderer component (ex.: ```StaticMeshRenderer```) will own an instance of a constant buffer with object data (```CBObject```) that is updated right before a new draw call is issued.

After these 2 constant buffers comes a read-only structured buffer with light data (```BufLights```), which is populated for each camera by the graphics stack. This buffer contains light color, intensity, and directional information for each light source that may illuminate the contents of a camera's viewport.

On Vulkan and Direct3D, the light buffer buffer is always bound to the first texture register. On MacOS/Metal, it is bound as a buffer right after the constant buffers.

| **HLSL Struct** | **Type** | **C\# Type** | **HLSL Register** | **MSL Index** |
| --- | --- | --- | --- | --- |
| ```CBGlobal``` | Constant Buffer | ```GlobalConstantBuffer``` | b0 | buffer(+1) |
| ```CBObject``` | Constant Buffer | ```ObjectDataConstantBuffer``` | b1 | buffer(+2) |
| ```BufLights``` | Structured Buffer (readonly) | ```Light.LightSourceData``` | t0 | buffer(+3) |


####User-bound Resources:

Any additional buffers may be bound by user code or the graphics stack after the above buffers' slots.

| **HLSL Buffer** | **Type** | **C\# Type** | **HLSL Register** | **MSL Index** |
| --- | --- | --- | --- | --- |
| ```[BufCustom]``` | Structured Buffer (readonly) | ```<T>``` | t1+ | buffer(+4) |
