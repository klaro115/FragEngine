using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragEngine3.Graphics
{
	public sealed class GraphicsRenderer
	{
		#region Constructors

		public GraphicsRenderer(GraphicsSystem _graphicsSystem)
		{
			graphicsSystem = _graphicsSystem ?? throw new ArgumentNullException(nameof(_graphicsSystem), "Graphics system may not be null!");
		}

		#endregion
		#region Fields

		public readonly GraphicsSystem graphicsSystem;

		#endregion
		#region Properties

		public bool IsDisposed => graphicsSystem.IsDisposed || graphicsSystem.graphicsCore.IsDisposed;
		public bool IsInitialized => graphicsSystem.graphicsCore.IsInitialized;

		#endregion
		#region Methods

		/// <summary>
		/// Add draw calls for rendering polygonal geometry to a command list.<para/>
		/// NOTE: '<see cref="CommandList.Begin"/>' must have been called and a target frame buffer set using
		/// '<see cref="CommandList.SetFramebuffer(Framebuffer)"/>' before this method is called.
		/// </summary>
		/// <param name="_cmdList">The command list to which we want to add the draw calls. If null, the main
		/// command list of the material's graphics core is used.</param>
		/// <param name="_vertexBuffer">The vertex buffer, may not be null.</param>
		/// <param name="_indexBuffer">The index buffer, may not be null, must contain at least 1 triangle.</param>
		/// <param name="_indexCount">The number of triangle indices to draw from the given index buffer. Must be
		/// a multiple of 3.</param>
		/// <param name="_indexFormat">The format of indices inside the given index buffer; either 16-bit
		/// unsigned integers, or 32-bit integers. The larger format is more memory-hungry, but allows for much
		/// larger geometry with more than 65K vertices.</param>
		/// <param name="_material">The material detailing shaders, pipeline states, textures and buffers. May
		/// not be null, must be created from the same graphics device as the command list.</param>
		/// <returns>True if the draw calls could be successfully written, false otherwise.</returns>
		public bool DrawGeometry(
			CommandList _cmdList,
			DeviceBuffer _vertexBuffer,
			DeviceBuffer _indexBuffer,
			uint _indexCount,
			IndexFormat _indexFormat,
			Material _material)
		{
			// Get command list:
			if (_cmdList == null)
			{
				GraphicsCore core = _material.graphicsCore ?? graphicsSystem.graphicsCore;
				_cmdList = core.MainCommandList;
			}

			// Get pipeline:
			if (!_material.GetOrUpdatePipeline(out Pipeline pipeline))
			{
				return false;
			}

			// Bind resources:
			_cmdList.SetPipeline(pipeline);
			_cmdList.SetVertexBuffer(0, _vertexBuffer);
			_cmdList.SetIndexBuffer(_indexBuffer, _indexFormat);

			// Issue draw call:
			_cmdList.DrawIndexed(_indexCount);
			return true;
		}

		#endregion
	}
}

