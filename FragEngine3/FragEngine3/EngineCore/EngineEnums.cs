using Veldrid;

namespace FragEngine3.EngineCore;

/// <summary>
/// The main operational stages of the engine.<para/>
/// NOTE: Application logic (such as scene updates, physics, and the sound system) other than configuration and setup
/// will only execute in stages '<see cref="Loading"/>', '<see cref="Running"/>', and '<see cref="Unloading"/>'.
/// </summary>
[Flags]
public enum EngineState
{
	None = 0,

	Startup = 1 << 1,
	Loading = 1 << 2,
	Running = 1 << 3,
	Unloading = 1 << 4,
	Shutdown = 1 << 5,
}

/// <summary>
/// An enumeration of flags for different properties that are specific to a platform or run-time environment.
/// These include the current operating system, and the graphics API that's used by the main graphics core.
/// </summary>
[Flags]
public enum EnginePlatformFlag
{
	None = 0,

	// Operating system:
	OS_Windows = 1,
	OS_MacOS = 2,
	OS_Linux = 4,
	OS_FreeBSD = 8,
	OS_Other = 16,

	// Graphics API:
	GraphicsAPI_D3D = 32,
	GraphicsAPI_Vulkan = 64,
	GraphicsAPI_Metal = 128,

	//...
}

/// <summary>
/// Helper class containing extension methods for the <see cref="EngineState"/> and <see cref="EnginePlatformFlag"/> enums.
/// </summary>
public static class EngineEnumsExt
{
	#region Constants

	/// <summary>
	/// A bit mask containing all engine states that run a main loop through which application logic may be run.
	/// </summary>
	public const EngineState allMainLoopEngineStates =
		EngineState.Loading |
		EngineState.Running |
		EngineState.Unloading;

	/// <summary>
	/// A bit mask of all platform flags that identify an operating system.
	/// </summary>
	public const EnginePlatformFlag allOperatingSystemFlags =
		EnginePlatformFlag.OS_Windows |
		EnginePlatformFlag.OS_MacOS |
		EnginePlatformFlag.OS_Linux |
		EnginePlatformFlag.OS_FreeBSD |
		EnginePlatformFlag.OS_Other;

	/// <summary>
	/// A bit mask of all platform flags that identify a graphics API.
	/// </summary>
	public const EnginePlatformFlag allGraphicsApiFlags =
		EnginePlatformFlag.GraphicsAPI_D3D |
		EnginePlatformFlag.GraphicsAPI_Vulkan |
		EnginePlatformFlag.GraphicsAPI_Metal;

	#endregion
	#region Methods

	/// <summary>
	/// Gets whether this engine state starts a main loop in which application logic can be executed.
	/// </summary>
	/// <param name="_engineState">This engine state.</param>
	/// <returns>True if the state runs a main loop, false otherwise.</returns>
	public static bool HasMainLoop(this EngineState _engineState)
	{
		return (_engineState & allMainLoopEngineStates) != 0;
	}

	/// <summary>
	/// Gets flags that allow identification of the operating system from these platform flags, by masking out all other flags.
	/// </summary>
	/// <param name="_platformFlags">These platform flags, typically drawn straight from <see cref="PlatformSystem.PlatformFlags"/>.</param>
	/// <returns>The operating flags.</returns>
	public static EnginePlatformFlag GetOperatingSystemFlags(this EnginePlatformFlag _platformFlags)
	{
		return _platformFlags & allOperatingSystemFlags;
	}

	/// <summary>
	/// Gets flags that allow identification of the graphics API from these platform flags, by masking out all other flags.
	/// </summary>
	/// <param name="_platformFlags">These platform flags, typically drawn straight from <see cref="PlatformSystem.PlatformFlags"/>.</param>
	/// <returns>The graphics API flags.</returns>
	public static EnginePlatformFlag GetGraphicsFlags(this EnginePlatformFlag _platformFlags)
	{
		return _platformFlags & allGraphicsApiFlags;
	}

	/// <summary>
	/// Gets a graphics backend enum value corresponding to the platform flags.
	/// </summary>
	/// <param name="_platformFlags">These platform flags, typically drawn straight from <see cref="PlatformSystem.PlatformFlags"/>.</param>
	/// <returns>A graphics backend enum value.</returns>
	public static GraphicsBackend GetGraphicsBackend(this EnginePlatformFlag _platformFlags)
	{
		EnginePlatformFlag graphicsFlags = _platformFlags & allGraphicsApiFlags;

		return graphicsFlags switch
		{
			EnginePlatformFlag.GraphicsAPI_D3D => GraphicsBackend.Direct3D11,
			EnginePlatformFlag.GraphicsAPI_Vulkan => GraphicsBackend.Vulkan,
			EnginePlatformFlag.GraphicsAPI_Metal => GraphicsBackend.Metal,
			_ => GraphicsBackend.OpenGL,
		};
	}

	#endregion
}