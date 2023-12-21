namespace FragEngine3.EngineCore
{
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

    [Flags]
    public enum EnginePlatformFlag
    {
        None                    = 0,

		// Operating system:
		OS_Windows				= 1,
		OS_MacOS				= 2,
		OS_Linux				= 4,
		OS_FreeBSD				= 8,
        OS_Other                = 16,

		// Graphics API:
        GraphicsAPI_D3D			= 32,
        GraphicsAPI_Vulkan		= 64,
        GraphicsAPI_Metal		= 128,

		//...
    }
}
