using Veldrid;

namespace FragEngine3.Graphics
{
	public interface IRenderer : IDisposable
	{
		#region Properties

		/// <summary>
		/// Gets whether this renderer has been disposed and should no longer be used or referenced.
		/// </summary>
		bool IsDisposed { get; }
		/// <summary>
		/// Gets whether this renderer is currently visible and valid. Only renderers that return true
		/// will be asked to issue draw calls.
		/// </summary>
		bool IsVisible { get; }

		/// <summary>
		/// Which render mode/queue this renderer can be classified as. This is used by the layers of
		/// the graphcis stack to determine by when and in which order to draw renderers.
		/// </summary>
		public RenderMode RenderMode { get; }

		/// <summary>
		/// Gets the graphics core this renderer was created with.
		/// </summary>
		public GraphicsCore GraphicsCore { get; }

		#endregion
		#region Methods

		/// <summary>
		/// Requests the renderer to generate draw calls by writing them into the given command list.
		/// </summary>
		/// <param name="_cmdList">The command list that draw calls should be written into.</param>
		/// <returns>True if draw calls were issued, false otherwise.</returns>
		bool Draw(CommandList _cmdList);

		#endregion
	}
}
