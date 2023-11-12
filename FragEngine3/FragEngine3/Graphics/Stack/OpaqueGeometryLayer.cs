using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	[Obsolete($"Superseded by the {nameof(IGraphicsStack)} interface for more freeform modularity.")]
	public sealed class OpaqueGeometryLayer : GraphicsStackLayer
	{
		#region Constructors

		public OpaqueGeometryLayer(GraphicsStack _stack) : base(_stack)
		{
		}

		#endregion
		#region Fields

		private readonly List<IRenderer> renderers = new(256);

		#endregion
		#region Properties

		public override int RendererCount => renderers.Count;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsDisposed = true;

			renderers.Clear();
		}

		public override bool IsValid() => !IsDisposed;

		public override bool CanLayerBeParallelized(int _indexInStack, out int _outFirstCompatibleLayerIndex)
		{
			_outFirstCompatibleLayerIndex = _indexInStack;

			for (int i = _indexInStack - 1; i >= 0; i--)
			{
				// Only allow valid layers:
				GraphicsStackLayer layer = stack.layers[i];
				if (layer == null || layer.IsDisposed)
				{
					break;
				}

				// Check if type of layer is compatible:
				if (layer is OpaqueGeometryLayer /* ... */)
				{
					_outFirstCompatibleLayerIndex = i;
				}
				else
				{
					break;
				}
			}

			return _outFirstCompatibleLayerIndex < _indexInStack;
		}

		public override bool GetRenderTarget(out Framebuffer _outFramebuffer)
		{
			//TODO
			throw new NotImplementedException();
		}

		public override bool RebuildLayer(Scene _scene, List<GraphicsStackRendererHandle> _unassignedRenderers)
		{
			renderers.Clear();

			foreach (GraphicsStackRendererHandle handle in _unassignedRenderers)
			{
				if (handle.IsValid &&
					!handle.isAssigned &&
					handle.renderer.RenderMode == RenderMode.Opaque)
				{
					renderers.Add(handle.renderer);
					handle.isAssigned = true;
				}
			}

			return true;
		}

		public override bool DrawLayer(Scene _scene, CommandList _cmdList)
		{
			//TODO
			throw new NotImplementedException();
		}

		#endregion
	}
}

