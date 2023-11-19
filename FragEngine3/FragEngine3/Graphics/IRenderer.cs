using System.Numerics;
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
		/// Gets or sets whether to only draw this renderer if all its resource dependencies have been
		/// fully loaded. If false, all resource dependencies that are not yet loaded will be loaded
		/// immediately right before first use.<para/>
		/// HINT: Consider setting this to true for most background objects in a scene that are not
		/// actuvely important to gameplay or that can't be interacted with by the player. This provides
		/// a simple content streaming paradigm for load time performance optimization.
		/// </summary>
		bool DontDrawUnlessFullyLoaded { get; }

		/// <summary>
		/// Which render mode/queue this renderer can be classified as. This is used by the graphics
		/// stack to determine when and in which order to draw renderers.
		/// </summary>
		public RenderMode RenderMode { get; }
		/// <summary>
		/// Gets a bit flag representing a graphics layer that this renderer belongs to. Most objects
		/// in a scene should return 1, as that is the default layer for scene contents, though up to
		/// 31 additional layers may be used to mark content that should not be drawn by certain cameras.
		/// </summary>
		public uint LayerFlags { get; }

		/// <summary>
		/// Gets the graphics core this renderer was created with.
		/// </summary>
		public GraphicsCore GraphicsCore { get; }

		#endregion
		#region Methods

		/// <summary>
		/// Calculate a depth value that may be used for Z-sorting this renderer, for example when
		/// drawing it using an alpha-blended shader and material. The value should increase with
		/// distance from the viewport position.
		/// </summary>
		/// <param name="_viewportPosition">A viewport position in world space. Usually, this is the
		/// position where the currently rendering camera is located.</param>
		/// <param name="_cameraDirection">A vector indicating which direction the currently rendering
		/// camera is pointing. This is a normalized vector.</param>
		/// <returns></returns>
		public float GetZSortingDepth(Vector3 _viewportPosition, Vector3 _cameraDirection);

		/// <summary>
		/// Requests the renderer to generate draw calls by writing them into the given command list.
		/// </summary>
		/// <param name="_cmdList">The command list that draw calls should be written into.</param>
		/// <returns>True if draw calls were issued, false otherwise.</returns>
		bool Draw(CommandList _cmdList);

		#endregion
	}
}
