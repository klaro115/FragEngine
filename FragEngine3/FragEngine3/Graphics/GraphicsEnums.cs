﻿using Veldrid;

namespace FragEngine3.Graphics;

public enum WindowStyle
{
	Windowed,
	BorderlessFullscreen,
	Fullscreen
}

/// <summary>
/// Enumeration of different main rendering modes according to which content may be drawn.
/// </summary>
public enum RenderMode : int
{
	/// <summary>
	/// Pure computation task, may need to be completed before any geometry draw calls are processed.
	/// </summary>
	Compute			= 0,
	/// <summary>
	/// Opaque geometry, with depth testing and writing enabled. This should be most of your scene geonetry.
	/// </summary>
	Opaque,
	/// <summary>
	/// Alpha-blended geometry with transparency, with depth writing disabled. Graphics stack layers issuing
	/// draw calls for this render mode will likely do depth-sorting before the draw calls are generated.
	/// </summary>
	Transparent,
	/// <summary>
	/// Volumetric effects, such as clouds, fog, god rays, and other often atmospheric visual effects. This
	/// content is generally drawn after geometry passes, as many volumetric effects will rely on the final
	/// depth buffer results.
	/// </summary>
	Volumetric,
	/// <summary>
	/// Post-processing and screen-space effects, added after the scene has been fully composited.<para/>
	/// Examples: Depth of field, bloom, motion blur, and other visual effects that only apply to the scene,
	/// but not to the UI.
	/// </summary>
	PostProcessing_Scene,
	/// <summary>
	/// User-interface content should generally be drawn last, as it is composited on top of all scene
	/// visuals, and is usually not affected by post-processing and lighting effects.
	/// </summary>
	UI,
	/// <summary>
	/// Compositing the final image from the scene render and any UI or post-processing passes.
	/// </summary>
	Composition,
	/// <summary>
	/// Post-processing effects, applied to the final output after the scene, compositing, and UI have been fully rendered.<para/>
	/// Example: A blur effect that also affects the UI.
	/// </summary>
	PostProcessing_PostUI,
	/// <summary>
	/// Custom render order and processing methods, defined through user-side code. A specicialized layer
	/// must be added to the graphics stack to identify and handle these cases as the engine's built-in
	/// renderering systems won't know how to process them.
	/// </summary>
	Custom,
}

/// <summary>
/// Different types of camera projections.
/// </summary>
public enum CameraProjectionType
{
	/// <summary>
	/// Orthographic camera projection, where light rays reaching the camera all travel in parallel.
	/// </summary>
	Orthographic = 0,
	/// <summary>
	/// Perspective camera projection, where light rays reaching the camera all converge in the camera's position.
	/// </summary>
	Perspective
}

/// <summary>
/// Extension methods for dealing with the graphics system's enums.
/// </summary>
public static class GraphicsEnumsExt
{
	#region Fields

	private static readonly WindowState[] windowStates =
	[
		WindowState.Normal,
		WindowState.BorderlessFullScreen,
		WindowState.FullScreen
	];

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
