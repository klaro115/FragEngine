using FragEngine3.Graphics.Components;
using FragEngine3.Scenes;

namespace FragEngine3.Graphics.Stack;

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

	GraphicsCore Core { get; }

	#endregion
	#region Methods

	bool Initialize(Scene _scene);
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
	bool DrawStack(Scene _scene, List<IRenderer> _renderers, in IList<Camera> _cameras, in IList<Light> _lights);

	#endregion
}
