# FragEngine
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
Eventually, a proper asset pipeline is planned to replace and automate asset processing and bundling further.


### Platforms:

##### Windows:
Windows 10+ is currently the main development target, and all code will be developped for this platform first.
D3D11 ist a very developer-friendly API, therefore all feature will be completed and polished for Windows platforms first.

##### Linux:
Linux support is planned but not currently implemented. A vulkan backend and SPIR-V shaders are currently missing and may be added in the near future.

##### Apple:
Support for MacOS exists to some degree, but active development has been put on hold indefinitely.
The platform is reliant on its proprietary Metal API, which is so poorly documented and cumbersome to work with, that the tiem and effort it takes to port even basic functionality is just not worth it.
Support may be resumed in the future if express interest arises, and may possibly use MoltenVK, to bypass Apple's insufferable software ecosystem.
