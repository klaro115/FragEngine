
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public interface IGraphicsStack : IDisposable
	{
		#region Properties

		/// <summary>
		/// Gets whether the graphics stack has already been disposed.
		/// </summary>
		bool IsDisposed { get; }
		/// <summary>
		/// Gets whether this graphics stack is in a valid and operational state that can be initialized
		/// and used with the current settings.
		/// </summary>
		bool IsValid { get; }
		/// <summary>
		/// Gets whether the graphics stack is initialized and ready for immediate use.
		/// </summary>
		bool IsInitialized { get; }

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
		/// Retrieve the render targets drawn to by this stack.<para/>
		/// NOTE: It is possible to fetch and draw straight onto the swapchain's backbuffer,
		/// but this may conflict with other scenes attempting to do the same. If multiple
		/// scenes are active and rendering, consider rendering each to their own framebuffers
		/// and then compositing them afterwards.
		/// </summary>
		/// <param name="_outRenderTargets">Outputs the render targets drawn to by this stack.
		/// The render targets used here may be the backbuffer of the main swapchain. Null if
		/// the stack is not operational.</param>
		/// <returns>True if the stack is operational and a framebuffer was assigned, false
		/// otherwise.</returns>
		public bool GetRenderTargets(out Framebuffer? _outRenderTargets);

		bool DrawStack(Scene _scene);

		#endregion
	}
}

