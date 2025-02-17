using System.Numerics;

namespace FragEngine3.Graphics;

/// <summary>
/// Method delegate for window closing events. This is used by <see cref="GraphicsCore"/> and <see cref="GraphicsSystem"/>.
/// </summary>
/// <param name="_graphicsCore">The graphics core whose main window is in the process of closing.</param>
public delegate void FuncWindowClosing(GraphicsCore _graphicsCore);

/// <summary>
/// Method delegate for window resising events. This is used by <see cref="GraphicsCore"/> and <see cref="GraphicsSystem"/>.
/// </summary>
/// <param name="_graphicsCore">The graphics core whose main window was just resized.</param>
/// <param name="_previousSize">The previous window size, in pixels. Dimensions are negative if the previous size is not known.param>
/// <param name="_newSize">The new window size, in pixels.</param>
public delegate void FuncWindowResized(GraphicsCore _graphicsCore, Vector2 _previousSize, Vector2 _newSize);

