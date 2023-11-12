using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public sealed record GraphicsStackRendererHandle
	{
		public GraphicsStackRendererHandle(IRenderer _renderer)
		{
			renderer = _renderer;
		}

		public readonly IRenderer renderer;
		public bool isAssigned = false;

		public bool IsValid => renderer != null && !renderer.IsDisposed;
		public bool IsVisible => IsValid && renderer.IsVisible;
	}

	[Obsolete($"Superseded by the {nameof(IGraphicsStack)} interface for more freeform modularity.")]
	public abstract class GraphicsStackLayer : IDisposable
	{
		#region Constructors

		protected GraphicsStackLayer(GraphicsStack _stack)
		{
			stack = _stack ?? throw new ArgumentNullException(nameof(_stack), "Graphics stack may not be null!");
		}
		~GraphicsStackLayer()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsStack stack;

		#endregion
		#region Properties

		public bool IsDisposed { get; protected set; } = false;
		/// <summary>
		/// Gets whether the layer has any renderers bound to it. If true, the layer will be skipped
		/// during rendering.
		/// </summary>
		public bool IsEmpty { get; protected set; } = false;

		/// <summary>
		/// Gets the total number of renderers that have been claimed by and bound to this layer.
		/// </summary>
		public abstract int RendererCount { get; }

		protected Logger Logger => stack.core.graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		protected virtual void Dispose(bool _disposing)
		{
			IsDisposed = true;
		}

		public abstract bool IsValid();

		/// <summary>
		/// Check whether this layer of the stack can be parallelized and command lists populated on another
		/// thread in parallel to this layer by some or all of the preceding layers on the stack.
		/// </summary>
		/// <param name="_indexInStack">The index of this layer on the graphics stack.</param>
		/// <param name="_outFirstCompatibleLayerIndex">Outputs the index of the first layer on the stack up
		/// to which parallelization is possible. If no parallelization is possible, this index is invalid
		/// and should be ignored.</param>
		/// <returns>True if parallelization alongside any of the preceding layers is possible, false otherwise.</returns>
		public virtual bool CanLayerBeParallelized(int _indexInStack, out int _outFirstCompatibleLayerIndex)
		{
			_outFirstCompatibleLayerIndex = _indexInStack;
			return false;
		}

		/// <summary>
		/// Gets one or more frame buffers that serve as render targets for this layer.<para/>
		/// NOTE: During rebuilding, it is possible for multiple layers to use the same render targets,
		/// such as an opaque rendering pass and a z-sorted alpha-blended pass both rendering a same
		/// final image, though typically not executing simultaneously.
		/// </summary>
		/// <param name="_outFramebuffer">Outputs thenframe buffer that will be drawn to by this layer. This will
		/// not be null and must contain at least one valid render target.</param>
		/// <returns></returns>
		public abstract bool GetRenderTarget(out Framebuffer _outFramebuffer);

		/// <summary>
		/// Rebuilds or reinitializes this layer, and binds renderers to it.
		/// </summary>
		/// <param name="_scene">The scene that will be drawn using the graphics stack, must be non-null.<para/>
		/// NOTE: A layer does not need to access or reference the scene. In most situations, the data provided
		/// by the renderers themselves should be sufficient to determine whether and how to bind them to this
		/// layer.</param>
		/// <param name="_unassignedRenderers">List of all remaining renderers in the scene that have not yet
		/// been bound to or claimed by any of the preceding layers. This layer will mark any renderers it claims
		/// as assigned, subsequent layers may not claim assigned renderers.<para/>
		/// NOTE: If no renderers are claimed by this layer, the flag '<see cref="IsEmpty"/>' should be set to
		/// true, which will cause the layer to be skipped during rendering.</param>
		/// <returns>True if the layer was rebuilt successfully, and is ready for operation, false otherwise.</returns>
		public abstract bool RebuildLayer(Scene _scene, List<GraphicsStackRendererHandle> _unassignedRenderers);

		/// <summary>
		/// Draw the layer's bound renderers by issuing draw calls for each.
		/// </summary>
		/// <param name="_scene">The scene which the renderers originate from. This is provided mostly for
		/// reference and in case additional scene information is needed for specialized rendering operations.
		/// May not be null.</param>
		/// <param name="_cmdList">The command list to which all draw calls have been written. Parallelized layers
		/// will each draw to a different command list, which are submitted in the same order as the layers are
		/// placed within the graphics stack, but can be populated simultaneously.<para/>
		/// NOTE: The last layer's command list will generally be the graphics core's main command list, since
		/// it will be output directly to the screen.</param>
		/// <returns></returns>
		public abstract bool DrawLayer(Scene _scene, CommandList _cmdList);

		#endregion
	}
}

