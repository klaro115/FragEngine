namespace FragEngine3.Graphics.Config;

[Serializable]
public sealed class GraphicsConfig
{
	#region Properties

	// WINDOW & DEVICE:

	public bool PreferNativeFramework { get; set; } = true;
	public WindowStyle WindowStyle { get; set; } = WindowStyle.Windowed;
	public bool AllowWindowResize { get; set; } = false;
	public bool AllowWindowClose { get; set; } = true;
	public int DisplayIndex { get; set; } = 0;
	public bool CenterWindowOnScreen { get; set; } = true;
	//...

	// OUTPUT & SWAPCHAIN:

	public int OutputBitDepth { get; set; } = 8;
	public bool OutputIsSRGB { get; set; } = false;
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

