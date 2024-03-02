# Fragment Engine
Cross-platform 3D game engine written in C# using .NET 8.

The engine is designed to be open-source, and available for both free and commercial usage. It is provided under the [Apache 2.0](https://github.com/klaro115/FragEngine?tab=Apache-2.0-1-ov-file#readme) license.


### Dependencies:
The number of third-party dependencies for this engine is designed to be as low as possible.
As much of the code and functionality as possible are written custom for this project, and implemented in pure C#.

The main dependency at this time is [Veldrid](https://veldrid.dev/), a cross-platform wrapper for graphics APIs, which is used as the basis for the engine's graphics, input, and window management modules.

Beyond that, the [Magick.NET](https://github.com/dlemstra/Magick.NET) library was added for image file import. A small number of file formats will still see custom implementations for this project.


### Assets:
An assets folder exists within the root directory, called "data". This folder must be copied into the build results directory.
Scripts for Windows and MacOS are included, to automate this copying as much as possible.
Eventually, a proper asset pipeline is planned, to replace and automate asset processing and bundling further.


### Platforms:

#### Windows:
Windows 10+ is currently the main development target, and all code will be developped for this platform first.
D3D11 being a very developer-friendly API, all features will be completed and polished with priority for Windows platforms.

#### Linux:
Linux support is planned but not currently implemented. A Vulkan backend and SPIR-V shaders are currently missing and may be added in the near future.

#### Apple:
Support for MacOS exists to some degree, but active development has been put on hold indefinitely.
The platform is reliant on its proprietary Metal API, which is so poorly documented and cumbersome to work with, that the time and effort it takes to implement even basic functionality is just not worth it.
Support may be resumed in the future if express interest arises, and may possibly use MoltenVK, to bypass Apple's insufferable software ecosystem.


### Roadmap:

The following is a rough and very short-sighted roadmap of features that are going to be added in the near future. The order of implementation may be subject to change.

- Rework engine state machine:
    - Split states into their own classes
    - More granular thread sleep timings
- Refactor Forward+Lights graphics stack
- Post-processing:
    - Implement test stacks
    - Common VFX modules
- Animation system:
    - Support for 3D files that allow animations
    - Non-static mesh renderers
    - Blend shape logic
    - Bone animation logic
- Asset importer/bundler toolchain
- Add file format support:
    - QOI: needs proper testing
    - FBX (geometry, blend shapes, bone animation)
    - GLTF
    - ...
- Physics engine (maybe Bullet Physics?)
- Particle systems
- Additional I/O support:
    - Webcams (probably via WMF on Windows?)
    - Gamepads
- Linux support: (distant-ish future)
    - Vulkan graphics core and shaders
