using Veldrid;

namespace FragEngine3.Graphics
{
	public enum WindowStyle
	{
		Windowed,
		BorderlessFullscreen,
		Fullscreen
	}

	public static class GraphicsEnumsExt
	{
		#region Fields

		private static readonly WindowState[] windowStates = new WindowState[]
		{
			WindowState.Normal,
			WindowState.BorderlessFullScreen,
			WindowState.FullScreen
		};

		#endregion
		#region Methods

		public static WindowState GetVeldridWindowState(this WindowStyle _style)
		{
			if (_style >= 0 && (int)_style < 3)
			{
				return windowStates[(int)_style];
			}
			return WindowState.Normal;
		}

		#endregion
	}
}
