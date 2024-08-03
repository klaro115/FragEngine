<h1>About Tools</h1>

The folder containing this markdown file contains a number of tools that may be used by the engine's toolchain and asset pipeline to help process data and files for use in your apps.

The only reason the tools were added to this repo directly, was to flatten the learning curve by making setup of the development environment as streamlined as possible.
Nobody in their right mind wants to build commandline tools from source, unless they are looking at using them in a very exotic environment, or because they need highly specific optional features that are disabled by default.
FragEngine does not have any such requirments and will therefore provide generic vanilla builds of these tools, ready for immediate use.

Do note that some of these tools may only be available on certain platforms. This is annoying, but ultimately out of our hands, since we only have so much time and resources to develop our own low-level tools.

<br>


## Legal Disclaimer
Most items in this folder and its subdirectories are thrid-party executables. All rights and legal matters remain with the original authors. Fragment engine and its authors do not claim authership, ownership, or any further rights to this software beyond what their respective licenses permit.
Original license documents are included with each software.

If you represent the authors or owners of any of the included pieces of software, and you do not wish to see them distributed here, please contact the authors of the Fragment Engine so that we may remove tham from this repository ASAP.

<br>


## DXC - DirectX Shader Compiler
_Source Repo:_ [GitHub Repo](https://github.com/microsoft/DirectXShaderCompiler)<br>
_How to use:_ [GitHub Docs](https://github.com/Microsoft/DirectXShaderCompiler/blob/main/tools/clang/docs/UsingDxc.rst)

The DirectX shader compiler is a Windows app that can be used to compile HLSL shaders to DXIL (**D**irect**X** **I**ntermediate **L**anguage) or SPIR-V (Vulkan intermediate shader language). The app can be built for other environments and platforms as well, but compiling the repo from source was left as an execise for users who might need it. The included version of DXC supports x86_64 only, and targets D3D11, D3D12, and Vulkan.

### Basic Usage
Basic shader compilation using DXC can be done via powershell using a variation of the following command:
```
$    ./dxc.exe -T <shader_model> -E <entry_point> -I <include_file_path> <shader_file_path>
```

<ins>Shader model:</ins><br>
A combination of the shader type and the shader model version, separated by underscores. Example: `ps_6_3`
- Shader types:
  - `vs` => Vertex
  - `ps` => Pixel
  - `cs` => Compute
- Model versions are split into major and minor version:
  - Format: `6_0` => 6.0, `5_1` => 5.1, `6_5` => 6.5
  - Models above 6.6 might not be supported by D3D11.

<ins>Entry point:</ins><br>
The name of the entry point function for this shader program.

<ins>Include file path:</ins><br>
Additional HLSL source code file that may be included when compiling the shader. This may be file containing lighting logic that is shared across multiple shader programs, but needs only be implemented once. This flag is optional.

<ins>Shader file path:</ins><br>
The path to the HLSL shader code file that you want to compile.


### SPIR-V Code Generation
The DXC app is also capable of compiling your HLSL source code straight to SPIR-V code, which may be used with a Vulkan graphics backend.

```
$    ./dxc.exe <...> -spirv <shader_file_path>
```

<ins>SPIRV-V flag:</ins><br>
To compile your shader code to SPIR-V instead of DXIL, you may add the `-spirv` keyword into command line instruction. All other flags and instructions can be the same as for DXIL compilation.


### Additional Options
Your usage of DXC may be further customized using any of a myriad other command line flags.
For a complete list of compiler flags, check out the docs or have a look at:
```
$    ./dxc.exe -help
```
