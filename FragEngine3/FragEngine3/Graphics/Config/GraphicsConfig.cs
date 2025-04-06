namespace FragEngine3.Graphics.Config;

[Serializable]
public sealed class GraphicsConfig
{
	#region Properties

	// WINDOW & DEVICE:

	public bool PreferNativeFramework { get; init; } = true;
	public WindowStyle WindowStyle { get; init; } = WindowStyle.Windowed;
	public bool AllowWindowResize { get; init; } = false;
	public bool AllowWindowClose { get; init; } = true;
	public int DisplayIndex { get; init; } = 0;
	public bool CenterWindowOnScreen { get; init; } = true;
	//...

	// OUTPUT & SWAPCHAIN:

	public int OutputBitDepth { get; init; } = 8;
	public bool OutputIsSRGB { get; init; } = false;
	//...

	// SETTINGS:

	public GraphicsSettings FallbackGraphicsSettings { get; set; } = new();
	//...

	#endregion
	#region Methods

	public GraphicsConfig Clone()
	{
		FallbackGraphicsSettings ??= new();

		return new()
		{
			// WINDOW & DEVICE:

			PreferNativeFramework = PreferNativeFramework,
			WindowStyle = WindowStyle,
			AllowWindowResize = AllowWindowResize,
			AllowWindowClose = AllowWindowClose,
			DisplayIndex = DisplayIndex,
			CenterWindowOnScreen = CenterWindowOnScreen,
			//...

			// OUTPUT & SWAPCHAIN:

			OutputBitDepth = OutputBitDepth,
			OutputIsSRGB = OutputIsSRGB,
			//...

			// SETTINGS:

			FallbackGraphicsSettings = FallbackGraphicsSettings.Clone(),
			//...
		};
	}

	#endregion
}

