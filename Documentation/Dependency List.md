<h1>Dependency List</h1>

The following is a list of all direct code dependencies that the Fragment engine relies on, for each project in the solution:

- [_Lib:_ FragEngine3](#lib-fragengine3)
    - [Frameworks](#frameworks)
    - [NuGet Packages](#nuget-packages)
- [_Exe:_ FragAssetPipeline](#exe-fragassetpipeline)
    - [Frameworks](#frameworks-1)
    - [Projects](#projects)
    - [NuGet Packages](#nuget-packages-1)
- [_Exe:_ TestApp](#exe-testapp)
    - [Frameworks](#frameworks-2)
    - [Projects](#projects-1)

<br>


## _Lib:_ FragEngine3

#### Frameworks
- .NET 8.0

#### NuGet Packages
- [Magick.NET-Q16-AnyCPU](https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU) `14.0.0`
- [NativeLibraryLoader](https://www.nuget.org/packages/NativeLibraryLoader) `1.0.13`
- [Veldrid](https://www.nuget.org/packages/Veldrid/) `4.9.0`
- [Veldrid.MetalBindings](https://www.nuget.org/packages/Veldrid.MetalBindings) `4.9.0`
- [Veldrid.SDL2](https://www.nuget.org/packages/Veldrid.SDL2) `4.9.0`
- [Veldrid.SPIRV](https://www.nuget.org/packages/Veldrid.SPIRV) `1.0.15`
- [Veldrid.StartupUtilities](https://www.nuget.org/packages/Veldrid.StartupUtilities) `4.9.0`

<br>


## _Exe:_ FragAssetPipeline

#### Frameworks
- .NET 8.0

#### Projects
- [_Lib:_ FragEngine3](#lib-fragengine3)
    - [Frameworks](#frameworks)
    - [NuGet Packages](#nuget-packages)
- [_Exe:_ FragAssetPipeline](#exe-fragassetpipeline)
    - [Frameworks](#frameworks-1)
    - [Projects](#projects)
    - [NuGet Packages](#nuget-packages-1)
- [_Exe:_ TestApp](#exe-testapp)
    - [Frameworks](#frameworks-2)
    - [Projects](#projects-1)

#### NuGet Packages
- [Vortice.Dxc](https://www.nuget.org/packages/Vortice.Dxc/3.6.0-beta) `3.5.0`

<br>


## _Exe:_ TestApp

#### Frameworks
- .NET 8.0

#### Projects
- [_Lib:_ FragEngine3](#lib-fragengine3)
    - [Frameworks](#frameworks)
    - [NuGet Packages](#nuget-packages)
- [_Exe:_ FragAssetPipeline](#exe-fragassetpipeline)
    - [Frameworks](#frameworks-1)
    - [Projects](#projects)
    - [NuGet Packages](#nuget-packages-1)
- [_Exe:_ TestApp](#exe-testapp)
    - [Frameworks](#frameworks-2)
    - [Projects](#projects-1)