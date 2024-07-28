<h1>Fragment Engine</h1>
Cross-platform 3D game engine written in C# using .NET 8.

The engine is designed to be open-source, and available for both free and commercial usage. It is provided under the [Apache 2.0](https://github.com/klaro115/FragEngine?tab=Apache-2.0-1-ov-file#readme) license. See [License](./LICENSE) file for up-to-date info.

<h3>Table of Contents:</h3>

- [Development State](#development-state)
- [Dependencies](#dependencies)
- [Architecture](#architecture)
- [Assets](#assets)
    - [Resource Files](#resource-files)
    - [Shaders \& Graphics](#shaders--graphics)
- [Platforms](#platforms)
    - [Windows](#windows)
    - [Apple](#apple)
    - [Linux](#linux)
    - [Mobile (iOS/Android)](#mobile-iosandroid)
- [Work In Progress](#work-in-progress)
- [Roadmap](#roadmap)

<br>


## Development State
The engine is in active development and is not yet in a usable state for game development. The graphics system is presently the main focal point, with basic asset support a close second. Once graphics have been polished to a serviceable level, work will shift towards creating a UI system. Only after these main systems enter a functional state should the engine be considered for use in any kind of software development.

<br>


## Dependencies
The number of third-party dependencies for this engine is designed to be as low as possible.
As much of the code and functionality as possible are written custom for this project, and implemented in pure C#.

The main dependency at this time is [Veldrid](https://veldrid.dev/), a cross-platform wrapper for graphics APIs, which is used as the basis for the engine's graphics, input, and window management modules.

Beyond that, the [Magick.NET](https://github.com/dlemstra/Magick.NET) library was added for image file import. A small number of file formats will still see custom implementations for this project.

<br>


## Architecture
The engine is designed with multithreading in mind. To this end, there are 3 main ways for developers to implement application logic using this engine. These include a component system, with behaviours attached to individual nodes in the scene, but also 2 interface points for driving application-level or scene-level logic without the need for the engine's node/component system.

For more information on how to implement app logic, see the [Architecture Guide](./FragEngine3/Documentation/Architecture.md).

<br>


## Assets
An assets folder exists within the root directory, called "data". This folder must be copied into the build results directory.
Scripts for Windows and MacOS are included, to automate this copying as much as possible.
Eventually, a proper asset pipeline is planned, to replace and automate asset processing and bundling further.

#### Resource Files

Resources are primarily loaded from files located within the "data" folder. Each resource file might contain one or more resources, and must be accompanied by a descriptive resource metadata file. You may refer to the [Resource Guide](./FragEngine3/Documentation/Resources/Resource%20Guide.md) for more details on asset management.

#### Shaders & Graphics

Standard shaders exist in HLSL (Direct3D shading language) in the most up-to-date form, and in MSL (Metal shading language) in an out-dated form.
The following document provides a rough overview of default resource bindings and pipeline behaviour: [Shader Guide](./FragEngine3/Documentation/Graphics/Shader%20Guide.md)

<br>


## Platforms

#### Windows
_Support: up-to-date_ ✅<br>
Windows 10+ is currently the main development target, and all code will be developed for this platform first.
D3D11 being a very developer-friendly API, all features will be completed and polished with priority for Windows platforms.

#### Apple
_Support: partial_ 🛠️<br>
Support for MacOS exists to some degree, but active development has been put on hold.
The platform is reliant on its proprietary Metal API, which is so poorly documented and cumbersome to work with, that the time and effort it takes to implement even basic functionality is often just not worth it.
Support may be resumed in the future if express interest arises, and may possibly use MoltenVK, to bypass Apple's insufferable software ecosystem.

#### Linux
_Support: not implemented_ ❌<br>
Linux support is planned but not currently implemented. A Vulkan backend and SPIR-V shaders are currently missing and may be added in the near future.

#### Mobile (iOS/Android)
_Support: not implemented_ ❌<br>
No support is planned for the foreseeable future. Android will have to wait until a Vulkan graphics backend is fully implemented, while iOS support hinges entirely on a functional Metal backend.

<br>


## Work In Progress

- Refactor graphics architecture:
    - Refactor Forward+Lights graphics stack _(split into sub-modules)_
- Lighting system: **[on hold]**
    - Indirect lighting **[awaiting graphics refactoring]**
- File format support: **[on hold]**
    - FBX _(3D geometry)_
    - DDS _(textures)_

<br>


## Roadmap

The following is a rough and very short-sighted roadmap of features that are going to be added in the near future. The order of implementation may be subject to change.

- Refactor graphics architecture:
    - Add rendering groups for auto-parallelizing draw call creation
- Engine systems:
    - Job scheduling system _(for asynchronous workloads)_
- UI:
    - Text rendering
    - Basic UI controls _(labels, buttons, etc.)_
    - Layouting groups
    - Touch support _(much later, required for mobile platforms)_
- Rework engine state machine:
    - More granular thread sleep timings
- Post-processing:
    - Implement standard/demo stack
    - Common VFX modules _(blur, bloom, DoF, etc.)_
- Animation system:
    - Support for 3D files that allow animations
    - Non-static mesh renderers
    - Blend shape logic
    - Bone animation logic
- Asset importer/bundler toolchain
- File format support:
    - FBX _(blend shapes, bone animation)_
    - GLTF
    - ...
- Physics engine _(maybe Bullet Physics?)_
- Particle systems
- Additional I/O support:
    - Webcams _(probably via WMF on Windows?)_
    - Gamepads
    - Touchscreens
- Linux support: _(distant-ish future)_
    - Vulkan graphics core
    - Vulkan/SPIR-V shaders
