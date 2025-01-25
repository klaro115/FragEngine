<h1>FSHA - Shader Format Specification</h1>

_Format version: 0.2_

- [About](#about)
- [Structure](#structure)
  - [1. File Header](#1-file-header)
  - [2. Description](#2-description)
    - [ShaderDescriptionSourceCodeData Class](#shaderdescriptionsourcecodedata-class)
    - [VariantEntryPoint Class](#variantentrypoint-class)
    - [SourceCodeBlock Class](#sourcecodeblock-class)
    - [ShaderDescriptionVariantData Class](#shaderdescriptionvariantdata-class)
    - [ShaderLanguage Enum](#shaderlanguage-enum)
    - [CompiledShaderDataType Enum](#compiledshaderdatatype-enum)
    - [MeshVertexDataFlags Enum](#meshvertexdataflags-enum)
  - [3. Source Code](#3-source-code)
  - [4. Compiled Data](#4-compiled-data)

<br>


## About
The FSHA file format is a shader asset format developed for the Fragment engine. The file format extension ".fsha" is quite obviously short for "**F**ragment **Sha**der". It was created to allow for more complex loading and processing of shader programs, and to remove as much of the compilation process as possible from the app's run-time processing load.

<br>


## Structure
_Source code:_ [ShaderData](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/ShaderData.cs)<br>
The format consists of multiple sections, some of which are designed to be human-readable through an ordinary text editor, both to make debugging easier, and to enable modders to easily create their own contents for a game made using the engine. The following is a list of the main sections of the format:

| Section Name  | Size     | Data Format             | Description                                       | Required |
| ------------- | -------- | ----------------------- | ------------------------------------------------- | -------- |
| File Header   | 55 bytes | Fixed-length ASCII code | Format identifier, version, and sections map.     | yes      |
| Extra Headers | ?        | Fixed-length ASCII code | Additional headers, reserved for future versions. | ?        |
| Description   | variable | JSON (UTF-8)            | Description of file content and shader type.      | yes      |
| Source Code   | variable | HLSL, GLSL, MSL (UTF-8) | Full original shader code, typically in HLSL.     | no       |
| Compiled Data | variable | IL byte code            | Multiple blocks of compiled shader data.          | no       |

The file header contains a file format version number. For future versions of the format, it is possible that additional headers or sections may be added to the format's structure. These may be mapped directly within the extra headers section, and have their data located in new sections after the compiled data section. All minor versions within a same major version should remain interoperable and garantee at least basic functionality.

While there is nothing stopping anyone from packing all sections of the format as tightly as possible, the implementation for the Fragment engine uses the ASCII string `"\r\n######\r\n"` to separate them. While this adds around 50 bytes to average file size, it makes the files more human-readable.



### 1. File Header
_Source code:_ [FshaFileHeader](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/Internal/FshaFileHeader.cs)<br>
The file header is presented as an ASCII-encoded string of characters with a fixed minimum byte size. It is always safe to read those first bytes as ASCII, after which will eventually follow the JSON-encoded description block. Note that the upper bound for the header string's length is given by the header's third value. The description's JSON may or may not follow immediately after the file header; there may be some padding or a separator string in-between sections.

The file header starts with a 4-byte / 4-character magic number that spells out the file format name `FSHA`. The characters are listed in byte-order, so that they may be read in natural order. The magic numbers are not packed into an endianness-dependent 32-bit word. The four characters serve as a definitive identifier for the shader asset file's content; if they do not have the exact content of "FSHA", the file is invalid and should not be parsed any further.

After the magic numbers follows a series of numbers, encoded as hexadecimal string characters, and separated by underscores. After the last number of the header follows a line break in Microsoft/Windows style (CRLF) consisting of the characters `\r\n`. After this line break, optional extra headers may follow using the same data encoding style.

Header example: `FSHA_10_0037_0275_02AE_0617_01_000008C5_000008C4`

| Part | Part Name               | Digits | Data Format   | Description                                                         |
| ---- | ----------------------- | ------ | ------------- | ------------------------------------------------------------------- |
| 0    | Magic numbers           | 4      | ASCII letters | Magic number string, must be `FSHA`. Serves as format identifier.   |
| 1    | Format version          | 2      | 2x UInt4      | Format version number; 1st digit is major version, 2nd is minor.    |
| 2    | JSON description offset | 4      | UInt16        | Offset of JSON description section from file start, in bytes.       |
| 3    | JSON description size   | 4      | UInt16        | Size of JSON description section, in bytes.                         |
| 4    | Source code offset      | 4      | UInt16        | Offset of HLSL source code section, in bytes. `0` if not included.  |
| 5    | Source code size        | 4      | UInt16        | Size of HLSL source code section, in bytes. `0` if not included.    |
| 6    | Compiled block count    | 2      | UInt8         | Number of compiled shader data blocks.                              |
| 7    | Compiled data offset    | 8      | UInt32        | Offset of compiled shader data section from file start, in bytes.   |
| 8    | Compiled data size      | 8      | UInt32        | Size of compiled shader data section from file start, in bytes.     |



### 2. Description
_Source code:_ [ShaderDescriptionData](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/Internal/ShaderDataDescription.cs)<br>
The shader description block is made up of a JSON-serialized representation of the class `ShaderDescriptionData`. The JSON string is written in UTF-8 encoding, and should therefore be human-readable in a normal text editor, and follows immediately after the file header (and any extra headers).

The JSON string does not necessarily start immediately after the header ends; refer to the `JSON description offset` in the file header to determine the actual starting point of the string. Also, you should not rely on null-terminators to locate the end of the JSON section, and instead only expect valid JSON out to the `JSON description size` in the file header.

| Property        | Type                             | Description                                                            |
| --------------- | -------------------------------- | ---------------------------------------------------------------------- |
| ShaderStage     | `Veldrid.ShaderStages`           | Shader pipeline stage that this file's shader programs belong to.      |
| MinCapabilities | `string`                         | String description of the shader's minimum feature set.<sup>1</sup>    |
| MaxCapabilities | `string`                         | String description of the shader's maximum feature set.<sup>1</sup>    |
| SourceCode      | `ShaderDataSourceCodeDesc[]?`    | [Optional] Descriptions of bundled source code.                        |
| CompiledBlocks  | `ShaderDataCompiledBlockDesc[]?` | [Optional] Array of all pre-compiled shader variants within this file. |

<sup>1</sup>_Descriptor strings for denoting minimum and maximum supported feature sets are encoded in the ShaderConfig string format. See the section on `ShaderConfig` in [Lighting&Shading Guide](../Graphics/Lighting&Shading%20Guide.md) for more details about these strings' format._


#### ShaderDataSourceCodeDesc Struct
_Source code:_ [ShaderDataSourceCodeDesc, ...](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/Internal/ShaderDataSourceCodeDesc.cs)<br>
This optional member of the JSON-encoded description provides details about the source code. It is comprised of an array of descriptions, one for each block of source code.
Each block description contains the following data:

| Property     | Type                  | Description                                                             |
| ------------ | --------------------- | ----------------------------------------------------------------------- |
| Language     | `ShaderLanguage`      | Enum value indicating the shading language of this source code block.   |
| VariantFlags | `MeshVertexDataFlags` | Enum bit flags of the different vertex datavariants that are supported. |
| Offset       | `ushort`              | Offset of source code block from start of source code data section.     |
| Size         | `ushort`              | Size of source code data block, in bytes.                               |
| EntryPoint   | `string`              | Name base of all entry point functions with the stage's source code.    |

#### ShaderDataCompiledBlockDesc Struct
_Source code:_ [ShaderDataCompiledBlockDesc](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/Internal/ShaderDataCompiledBlockDesc.cs)<br>

| Property              | Type                     | Description                                                        |
| --------------------- | ------------------------ | ------------------------------------------------------------------ |
| DataType              | `CompiledShaderDataType` | The type of compiled shader data, i.e. what API/backend.           |
| VariantFlags          | `MeshVertexDataFlags`    | Bit mask of all vertex data flags that apply to this variant.      |
| Capabilities          | `string`                 | Descriptor string of the variant's shader features.<sup>1</sup>    |
| Offset                | `uint`                   | Offset of compiled data block from start of compiled data section. |
| Size                  | `uint`                   | Size of compiled data block, in bytes.                             |
| EntryPoint            | `string`                 | Name of this variant's entrypoint function.                        |

#### ShaderLanguage Enum
_Source code:_ [ShaderLanguage](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/ShaderEnums.cs)

| Flag | Name   | Description                                                                                           |
| ---- | ------ | ----------------------------------------------------------------------------------------------------- |
|    1 | HLSL   | Microsoft's _Direct3D_ shader language. HLSL stands for **H**igh **L**evel **S**hader **L**anguage.   |
|    2 | Metal  | Apple's _Metal_ shader language, which is only supported on _MacOS_ and _iOS_ platforms.              |
|    4 | GLSL   | OpenGL shader language, supported by _OpenGL_ and _Vulkan_ graphics APIs.                             |

#### CompiledShaderDataType Enum
_Source code:_ [CompiledShaderDataType](../../FragEngine3/FragEngine3/Graphics/Resources/Shaders/ShaderEnums.cs)

| Flag | Name   | Description                                                                                           |
| ---- | ------ | ----------------------------------------------------------------------------------------------------- |
|    1 | DXBC   | Old-style Direct3D shader byte code. This format is equivalent to the output of `D3dCompiler.h`.      |
|    2 | DXIL   | Dx12-style Direct3D intermediate language, produced by the DXC (**D**irect**X** Shader **C**ompiler). |
|    4 | SPIR-V | Vulkan's portable intermediate shader code.                                                           |
|    8 | Metal  | Metal shader archive. This is a compiled MSL shader library.                                          |
|  128 | Other  | Unknown or unsupported compiled shader format. Variants of this type are skipped on import.           |

#### MeshVertexDataFlags Enum
_Source code:_ [MeshVertexDataFlags](../../FragEngine3/FragEngine3/Graphics/Resources/VertexDataTypes.cs)<br>

| Flag | Name                | Description                                                                    |
| ---- | ------------------- | ------------------------------------------------------------------------------ |
|    1 | BasicSurfaceData    | Basic vertex data, includes position, normals, and texture coordinates.        |
|    2 | ExtendedSurfaceData | Extended vertex data, includes tangents and secondary texture coordinates.     |
|    4 | BlendShapes         | Blend shape vertex data, includes indices and weights for each vertex.         |
|    8 | Animations          | Bone animation vertex data, includes bone indices and weights for each vertex. |

<br>


### 3. Source Code
The source code section is an optional data section which contains the source code that was used to compile the bundled shader variants. Source code must encoded as ASCII or UTF-8 plaintext.

This section should only be present and contain code if the `Source code offset` and `Source code size` properties in the file header are both non-zero, and if entry points and supported features were fully documented in the JSON description's `SourceCode` field.

The description section also contains byte sizes and offsets of the different blocks contained within this section. Each block contains the same or equivalent shader code logic, though each in a different shader language. This way, if source code needs to be recompiled at run-time, the shader importer may choose to compile from whichever language enjoys the best support on the current platform.

There are 3 reasons to include shader source code in your release build:
- Variant compilation at run-time
- Provide code reference for modding
- No support for pre-compiled shader programs on exotic hardware

If you need to bundle source code for run-time compilation, but you do not wish to make the files publicly visible, consider using encrypted resource data files. Inside of an encrypted and compressed resource file, all assets contained therein should be safe from leaking unless users can gain access to the decryption key. See [Resource Guide](../Resources/Resource%20Guide.md) for details.

Note that source code must be bundled as a single monolithic string. Any and all include logic - linking in additional source code files - is not supported at this time. When in doubt, consider copy-pasting your included/shared sourc files (ex.: "lighting.hlsl") before the start of a specific shader's source code (i.e.: prefix "lighting.hlsl" to "pixel_shader.hlsl").

### 4. Compiled Data
The compiled data section consists of a contiguous sequence of byte data blocks. Each block contains pre-compiled shader programs for a specific variant of a shader asset, and targeting a specific graphics API.

The data in each compiled block may or may not be human-readable, seeing as the data is primarily in some portable intermediate language for faster on-the-fly transpiling to native machine code. When viewing an FSHA file in a common text editor, all contents from this section onwards will look like a garbled mess, and do not have any visible delimitations or markers to tell where one block ends and the next one starts. If the compiled data contains a string terminator value at some point, then the data thereafter may not show up at all, making it look like the file was cut short.

The shader importer will automatically pick the right pre-compiled block based on the underlying shader variant, and targeting the right graphics backend. If none of the pre-compiled variants match the importer's needs, it may instead use bundled source code to compile variants on-demand and at run-time.

Note that a valid FSHA file _must_ contain either source code or compiled data, but it can contain both. Files that lack either are invalid and should be discarded immediately by the importer.
