using FragEngine3.Graphics.Components;
using FragEngine3.Scenes;

namespace FragEngine3.Graphics.Stack;

/// <summary>
/// Base interface for classes that are used to render a scene's graphics. The stack governs
/// how the scene looks, and how its contents are composited for final output. Ownership of
/// a graphics stack lies with the scene it was assigned to during first initialization.
/// </summary>
public interface IGraphicsStack : IDisposable
{
	#region Properties

	/// <summary>
	/// Gets whether the graphics stack has already been disposed.
	/// </summary>
	bool IsDisposed { get; }
	/// <summary>
	/// Gets whether this graphics stack is in a valid and operational state that can be
	/// initialized and used with the current settings.
	/// </summary>
	bool IsValid { get; }
	/// <summary>
	/// Gets whether the graphics stack is initialized and ready for immediate use.
	/// </summary>
	bool IsInitialized { get; }
	/// <summary>
	/// Gets whether the graphics stack is currently in the processing of generating and
	/// pushing graphics commands.
	/// </summary>
	bool IsDrawing { get; }

	/// <summary>
	/// The graphics core that this graphics stack was created for.
	/// </summary>
	GraphicsCore Core { get; }

	#endregion
	#region Methods

	/// <summary>
	/// Initialized the stack for a given scene.
	/// </summary>
	/// <param name="_scene">The scene that shall be rendered using this graphics stack.</param>
	/// <returns>True if the stack was successfully initialized, false otherwise.</returns>
	bool Initialize(Scene _scene);
	/// <summary>
	/// Shuts down the stack and releases all resources tied to the scene's contents.
	/// </summary>
	void Shutdown();

	/// <summary>
	/// Reset all states of the stack, to return it to a clean operational state.
	/// If the graphics stack was previously initialized, calling this should return it
	/// to a state as if it was freshly created and initialized.<para/>
	/// HINT: A valid default implementation would be to shut down and then initialize
	/// the instance again, which should revert it to a basic operational state.
	/// </summary>
	/// <returns>True if the reset concluded successfully, false otherwise. On failure,
	/// the <see cref="IsValid"/> and <see cref="IsInitialized"/> flags may need to be
	/// checked again.</returns>
	bool Reset();

	/// <summary>
	/// Draw all renderers within a scene.
	/// </summary>
	/// <param name="_scene">The scene which we want to render, may not be null.</param>
	/// <param name="_renderers">A list of all renderers within the given scene. May not
	/// be null.</param>
	/// <param name="_cameras">List of all camera components within the scene.</param>
	/// <param name="_lights">List of all light components within the scene.</param>
	/// <returns>True if the scene and its nodes were rendered successfully, false
	/// otherwise.</returns>
	bool DrawStack(Scene _scene, List<IRenderer> _renderers, in IList<CameraComponent> _cameras, in IList<ILightSource> _lights);

	#endregion
}
