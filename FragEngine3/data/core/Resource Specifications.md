<h1>Resource File Specifications</h1>

The following document outlines requirements and specifications for some of the most important resource file formats used by the engine. See the [Resource Guide](./Resource%20Guide.md) for a more general outline of the resource/asset system.

- [ResourceFileData](#resourcefiledata)
- [ResourceHandleData](#resourcehandledata)
- [ResourceType Enum](#resourcetype-enum)
- [EnginePlatformFlag Enum](#engineplatformflag-enum)

<br>

## ResourceFileData
This is the immediate JSON-serialized data type of all ".fres" metadata files. The JSON is structured as follows:

| Field:                  | Type:                  | Description:                                                        |
| ----------------------- | ---------------------- | ------------------------------------------------------------------- |
| `DataFilePath`:         | `string`               | Relative path to the data file containing the resources' data.      |
| `DataFileType`:         | `ResourceFileType`     | Type of resource file:<br> 0=`Single`<br> 1=`Batch_Compressed`<br> 2=`Batch_BlockCompressed` |
| `DataFileSize`          | `ulong`                | Byte size of the data file. If 0, file size will be measured at launch. |
| `UncompressedFileSize`  | `ulong`                | Byte size of the uncompressed data file contents. Must be non-zero for compressed file types. |
| `BlockSize`             | `ulong`                | Byte size of compression blocks, block-compressed file type only.   |
| `BlockCount`            | `uint`                 | Total number of compressed blocks, block-compressed file type only. |
| `ResourceCount`         | `uint`                 | Number of resources encoded in the data file. Must be equal to or less than the length of the `Resources` array. |
| `Resources`             | `ResourceHandleData[]` | Array of resources contained in the data file. If null and single-resource type, the one resource handle may be inferred from the file extension of `DataFilePath`. |
<br>


## ResourceHandleData
This is the immediate JSON-serialized data type of resource handles in metadata files' `Resources` array. The JSON for each element in the array is structured as follows:

| Field:            | Type:                | Description:                                                                |
| ----------------- | -------------------- | --------------------------------------------------------------------------- |
| `ResourceKey`     | `string`             | Unique identifier and name of the resource.                                 |
| `ResourceType`    | `ResourceType`       | The type of resource, possible values listed [below](#resourcetype-enum)    |
| `PlatformFlags`   | `EnginePlatformFlag` | Binary flags for different platforms and API features required for this resource. If a flag is missing in the current app instance at start-time, the resource will be discarded. Possible values listed [below](#engineplatformflag-enum). |
| `ImportFlags`     | `string`             | A string containing additional instructions for the importer.               |
| `DataOffset`      | `ulong`              | Byte offset of the resource's data from the start of the data file, compressed file types only. |
| `DataSize`        | `ulong`              | Byte offset of the resource's data from the start of the data file, compressed file types only. |
| `DependencyCount` | `uint`               | Total number of immediate dependencies of the resource, that must be loaded before this resource can be loaded. |
| `Dependencies`    | `string[]`           | Array of resources keys of all resources that this resource is reliant on. May be null if the resource has no hard dependencies. |
<br>


## ResourceType Enum
An enum of different types of resources, differentiated by media type, usage, or data structure. Values are represented in JSON as integers.

| Value | Name        | Description                                                                                      |
| ----- | ----------- | ------------------------------------------------------------------------------------------------ |
|     0 | `Unknown`   | Unknown or unspecified data type. Placeholder value reserved to signify something went wrong.    |
|     1 | `Ignored`   | Internal and auto-generated files that should be ignored and skipped.                            |
|     2 | `Texture`   | Texture or image resource, can be 1D, 2D, 3D, a cubemap, or an array of textures.                |
|     3 | `Video`     | Video resource, can be any 2D rasterized animation.                                              |
|     4 | `Shader`    | A shader program, used by the GPU to process graphics resources, and usually to compute pixel data. |
|     5 | `Material`  | JSON object describing a composition of 1 or more shaders and various graphics resources such as textures. |
|     6 | `Animation` | Bone animation data for 3D models.                                                               |
|     7 | `Model`     | 3D object, typically in the form of polygonal geometry data and bone animation.                  |
|     8 | `Audio`     | Audio data of some kind. Can be either music, sound effects, etc.                                |
|     9 | `Prefab`    | A serialized object, encoding the data graph and dependencies for recreating a scene node from scratch. |
|    10 | `Font`      | A font style, for displaying text contents in UI or in the scene.                                |
|    11 | `Data`      | Any kind of raw or serialized data for use by miscellaneous systems. (ex.: JSON, CSV, TXT, etc.) |
|    12 | `Scene`     | A scene definition or a save file's full description of a scene's state.                         |
|    13 | `Script`    | A code or script file, possibly used to extend app logic, for automation, or for modding.        |
<br>


## EnginePlatformFlag Enum
An enum, used as bit mask, to signify the presence or requirement of different features in software or hardware. The most important platform flags include the graphics API, and the OS running the engine. Values are represented in JSON as integers.

| Flag | Name                 | Description                                                                              |
| ---- | -------------------- | ---------------------------------------------------------------------------------------- |
|    0 | `None`               | No platform requirements.                                                                |
|    1 | `OS_Windows`         | Microsoft _Windows_ operating system. The engine is targeting Windows 10 or newer.       |
|    2 | `OS_MacOS`           | Apple's _MacOS_ operating system. The engine is targeting Metal-capable versions.        |
|    4 | `OS_Linux`           | Some Linux distro, must work with SDL2 and support Vulkan graphics API.                  |
|    8 | `OS_FreeBSD`         | Some FreeBSD distro.                                                                     |
|   16 | `OS_Other`           | Miscellaneous other OS type. Support for these is questionable.                          |
|   32 | `GraphicsAPI_D3D`    | Microsoft's _Direct3D_ 11 graphics API. Standard API for the `OS_Windows` flag.          |
|   64 | `GraphicsAPI_Vulkan` | Khronos' _Vulkan_ graphics API. The go-to croos-platform graphics API.                   |
|  128 | `GraphicsAPI_Metal`  | Apple's _Metal_ graphics API. ~~Proprietary nonesense with an idiotic shader language.~~ |
<br>
