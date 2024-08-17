<h1>FSHA - Shader Format Specification</h1>

_Format version: 1.0_

- [About](#about)
- [Structure](#structure)
  - [1. File Header](#1-file-header)
  - [2. Description](#2-description)
    - [ShaderDescriptionSourceCodeData Class](#shaderdescriptionsourcecodedata-class)
    - [VariantEntryPoint Class](#variantentrypoint-class)
    - [ShaderDescriptionVariantData Class](#shaderdescriptionvariantdata-class)
    - [CompiledShaderDataType Enum](#compiledshaderdatatype-enum)
  - [3. Source Code](#3-source-code)
  - [4. Compiled Data](#4-compiled-data)

<br>


## About
The FSHA file format is a shader asset format developed for the Fragment engine. The file format extension ".fsha" is quite obviously short for "**F**ragment **Sha**der". It was created to allow for more complex loading and processing of shader programs, and to remove as much of the compilation process as possible from the app's run-time processing load.

<br>


## Structure
_Source code:_ [ShaderData](../../FragEngine3/FragEngine3/Graphics/Resources/Data/ShaderData.cs)<br>
The format consists of multiple sections, some of which are designed to be human-readable through an ordinary text editor, both to make debugging easier, and to enable modders to easily create their own contents for a game made using the engine. The following is a list of the main sections of the format:

| Section Name  | Size     | Data Format             | Description                                       | Required |
| ------------- | -------- | ----------------------- | ------------------------------------------------- | -------- |
| File Header   | 57 bytes | Fixed-length ASCII code | Format identifier, version, and sections map.     | yes      |
| Extra Headers | ?        | Fixed-length ASCII code | Additional headers, reserved for future versions. | ?        |
| Description   | variable | JSON (UTF-8)            | Description of file content and shader type.      | yes      |
| Source Code   | variable | HLSL, GLSL, MSL (UTF-8) | Full original shader code, typically in HLSL.     | no       |
| Compiled Data | variable | IL byte code            | Multiple blocks of compiled shader data.          | yes      |

The file header contains a file format version number. For future versions of the format, it is possible that additional headers or sections may be added to the format's structure. These may be mapped directly within the extra headers section, and have their data located in new sections after the compiled data section. All minor versions within a same major version should remain interoperable and garantee at least basic functionality.

While there is nothing stopping anyone from packing all sections of the format as tightly as possible, the implementation for the Fragment engine uses the ASCII string `"########\r\n"` to separate them. While this adds around 50 bytes to average file size, it makes the files ever so slightly more human-readable.



### 1. File Header
_Source code:_ [ShaderDataFileHeader](../../FragEngine3/FragEngine3/Graphics/Resources/Data/ShaderTypes/ShaderDataFileHeader.cs)<br>
The file header is presented as an ASCII-encoded string of characters with a fixed minimum byte size. It is always safe to read those first bytes as ASCII, after which will eventually follow the JSON-encoded description block. Note that the exact length of the header string is given by the header's third value, and that the description's JSON may or may not follow immediately after the file header.

The file header starts with a 4-byte / 4-character magic number that spells out the file format name `FSHA`. The characters are listed in byte-order, so that they may be read in natural order. The magic numbers are not packed into an endianness-dependent 32-bit word. The four characters serve as a definitive identifier for the shader asset file's content; if they do not have the exact content of "FSHA", the file is invalid and should not be parsed any further.

After the magic numbers follows a series of numbers, encoded as hexadecimal string characters, and separated by underscores. After the last number of the header follows a line break in Microsoft/Windows style (CRLF) consisting of the characters `\r\n`. After this line break, optional extra headers may follow using the same data encoding style.

Header example: `FSHA_10_0039_0039_0275_02AE_0617_0000_000008C5_000008C4`

| Part | Part Name               | Digits | Data Format   | Description                                                         |
| ---- | ----------------------- | ------ | ------------- | ------------------------------------------------------------------- |
| 0    | Magic numbers           | 4      | ASCII letters | Magic number string, must be `FSHA`. Serves as format identifier.   |
| 1    | Format version          | 2      | 2x UInt4      | Format version number; 1st digit is major version, 2nd is minor.    |
| 2    | Header size             | 4      | UInt16        | Total byte size of file header and extra headers. (Default: `0039`) |
| 3    | JSON description offset | 4      | UInt16        | Offset of JSON description section from file start, in bytes.       |
| 4    | JSON description size   | 4      | UInt16        | Size of JSON description section, in bytes.                         |
| 5    | Source code offset      | 4      | UInt16        | Offset of HLSL source code section, in bytes. `0` if not included.  |
| 6    | Source code size        | 4      | UInt16        | Size of HLSL source code section, in bytes. `0` if not included.    |
| 7    | Compiled block count    | 4      | UInt16        | Number of compiled shader data blocks.                              |
| 8    | Compiled data offset    | 8      | UInt32        | Offset of compiled shader data section from file start, in bytes.   |
| 9    | Compiled data size      | 8      | UInt32        | Size of compiled shader data section from file start, in bytes.     |



### 2. Description
_Source code:_ [ShaderDescriptionData, ...](../../FragEngine3/FragEngine3/Graphics/Resources/Data/ShaderTypes/ShaderDescriptionData.cs)<br>
The shader description block is made up of a JSON-serialized representation of the class `ShaderDescriptionData`. The JSON string is written in UTF-8 encoding, and should therefore be human-readable in a normal text editor, and follows immediately after the file header (and any extra headers).

The JSON string does not necessarily start immediately after the header ends; refer to the `JSON description offset` in the file header to determine the actual starting point of the string. Also, you should not rely on null-terminators to locate the end of the JSON section, and instead only expect valid JSON out to the `JSON description size` in the file header.

| Property         | Type                               | Description                                                       |
| ---------------- | ---------------------------------- | ----------------------------------------------------------------- |
| ShaderStage      | `Veldrid.ShaderStages`             | Shader pipeline stage that this file's shader programs belong to. |
| SourceCode       | `ShaderDescriptionSourceCodeData?` | [Optional] Description of bundled HLSL source code.               |
| CompiledVariants | `ShaderDescriptionVariantData[]`   | Array of all pre-compiled shader variants within this file.       |


#### ShaderDescriptionSourceCodeData Class
This optional member of the JSON-encoded description provides details about the source code. Most importantly, the names of entry point functions for each variant is listed, or a name base from which those variant names may be derived.

| Property                   | Type                   | Description                                                           |
| -------------------------- | ---------------------- | --------------------------------------------------------------------- |
| EntryPointNameBase         | `string`               | Name base of all entry point functions with the stage's source code.  |
| EntryPoints                | `VariantEntryPoint[]?` | [Optional] Array of all variants' entry points and vertex data flags. |
| SupportedFeaturesTxt       | `string`               | Descriptor string of all supported ShaderGen features.<sup>1</sup>    |
| MaximumCompiledFeaturesTxt | `string`               | Descriptor of most feature-complete compiled variant.<sup>1</sup>     |
| SourceCodeBlocks           | `SourceCodeBlock[]`    | Array of all source code blocks, each in a different language.        |

<sup>1</sup>_Descriptor strings for denoting supported and maximum feature sets are encoded in the ShaderGen format. See the section on `ShaderGenConfig` in [Lighting&Shading Guide](../Graphics/Lighting&Shading%20Guide.md) for more details about these strings' format._

#### VariantEntryPoint Class
This is a nested class within `ShaderDescriptionSourceCodeData`, which describes a specfic variant's entry point function. Each entry of this type is a possible variant that can be compiled at run-time from source code, should the need arise.

| Property     | Type                  | Description                                                           |
| ------------ | --------------------- | --------------------------------------------------------------------- |
| VariantFlags | `MeshVertexDataFlags` | Bit mask of all vertex data flags that apply to this variant.         |
| EntryPoint   | `string`              | Name of the entry point function of this variant.                     |

#### SourceCodeBlock Class
This is a nested class within `ShaderDescriptionSourceCodeData`, which describes one or more source code blocks that are bundled in the file. Each block contains the same or equivalent shader programl in a different shader language. By default, HLSL (Direct3D shading language) and MSL (Metal shading language) source code would be listed and bundled. Dependinhg on the language, the described source code block will be encoded in either UTF-8 or ASCII.

| Property     | Type                  | Description                                                           |
| ------------ | --------------------- | --------------------------------------------------------------------- |
| Language     | `ShaderGenLanguage`   | Enum value indicating the shading language of this source code block. |
| ByteOffset   | `uint`                | Offset of source code block from start of source code data section.   |
| ByteSize     | `uint`                | Size of source code data block, in bytes.                             |

#### ShaderDescriptionVariantData Class

| Property              | Type                     | Description                                                        |
| --------------------- | ------------------------ | ------------------------------------------------------------------ |
| Type                  | `CompiledShaderDataType` | The type of compiled shader data, i.e. what API/backend.           |
| VariantFlags          | `MeshVertexDataFlags`    | Bit mask of all vertex data flags that apply to this variant.      |
| VariantDescriptionTxt | `string`                 | Descriptor string of the variant's ShaderGen features.<sup>1</sup> |
| EntryPoint            | `string`                 | Name of this variant's entrypoint function within the source code. |
| ByteOffset            | `uint`                   | Offset of variant data block from start of compiled data section.  |
| ByteSize              | `uint`                   | Size of variant data block, in bytes.                              |

#### CompiledShaderDataType Enum
_Source code:_ [CompiledShaderDataType](../../FragEngine3/FragEngine3/Graphics/Resources/Data/ShaderTypes/ShaderDataEnums.cs)

| Flag | Name   | Description                                                                                           |
| ---- | ------ | ----------------------------------------------------------------------------------------------------- |
|    1 | DXBC   | Old-style Direct3D shader byte code. This format is equivalent to the output of `D3dCompiler.h`.      |
|    2 | DXIL   | Dx12-style Direct3D intermediate language, produced by the DXC (**D**irect**X** Shader **C**ompiler). |
|    4 | SPIR-V | Vulkan's portable intermediate shader code.                                                           |
|  128 | Other  | Unknown or unsupported compiled shader format. Variants of this type are skipped on import.           |

<br>


### 3. Source Code
The source code section is an optional data section which contains the source code that was used to compile the bundled shader variants. Source code must encoded as ASCII or UTF-8 plaintext.

This section should only be present and contain code if the `Source code offset` and `Source code size` properties in the file header are non-zero, and if entry points and supported features were fully documented in the JSON description's `SourceCode` field.

The description section also contains byte sizes and offsets of the different blocks contained within this section. Each block contains the same or equivalent shader code logic, though each in a different shader language. This way, if source code needs to be recompiled at run-time, the shader importer may choose to compile from whichever language enjoys the best support on the current platform.

There are 2 reasons to include shader source code in your release build:
- Variant compilation at run-time
- Provide code reference for modding

If you need to bundle source code for run-time compilation, but you do not wish to make the files publicly visible, consider using encrypted resource data files. Inside of an encrypted and compressed resource file, all assets contained therein should be safe from leaking unless users can gain access to the decryption key. See [Resource Guide](../Resources/Resource%20Guide.md) for details.

Note that source code must be bundled as a single monolothic string. Any and all include logic - linking in additional source code files - is not supported at this time. When in doubt, consider copy-pasting your included/shared sourc files (ex.: "lighting.hlsl") before the start of a specific shader's source code (i.e.: prefix "lighting.hlsl" to "pixel_shader.hlsl").

### 4. Compiled Data
The compiled data section consists of a contiguous sequence of byte data blocks. Each block contains pre-compiled shader programs for a specific variant of a shader asset, and targeting a specific graphics API.

The data in each compiled block may or may not be human-readable, seeing as the data is primarily in some portable intermediate language for faster on-the-fly transpiling to native machine code. When viewing an FSHA file in a common text editor, all contents from this section onwards will look like a garbled mess, and do not have any visible delimitations or markers to tell where one block ends and the next one starts. If the compiled data contains a string terminator value at some point, then the data thereafter may not show up at all, making it look like the file was cut short.

The shader importer will automatically pick the right pre-compiled block based on the underlying shader variant, and targeting the right graphics backend. If none of the pre-compiled variants match the importer's needs, it may instead use bundled source code to compile variants on-demand and at run-time.
