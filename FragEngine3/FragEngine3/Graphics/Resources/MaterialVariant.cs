using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public class MaterialVariant : IDisposable
	{
		#region Constructors

		public MaterialVariant(Material _material, MeshVertexDataFlags _vertexDataFlags)
		{
			material = _material ?? throw new ArgumentNullException(nameof(_material), "Material may not be null!");
			vertexDataFlags = _vertexDataFlags != 0 ? _vertexDataFlags : MeshVertexDataFlags.BasicSurfaceData;

			vertexLayoutDescs = CreateVertexLayouts();
		}
		~MaterialVariant()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Material material;
		public readonly MeshVertexDataFlags vertexDataFlags;
		private readonly VertexLayoutDescription[] vertexLayoutDescs;

		private Pipeline pipeline = null!;

		private GraphicsPipelineDescription pipelineDesc;
		//...

		private readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public Pipeline Pipeline => pipeline;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			IsDisposed = true;

			pipeline?.Dispose();
		}

		public bool UpdatePipeline(Material.DirtyFlags _dirtyFlags)
		{
			if (IsDisposed || material.IsDisposed)
			{
				Console.WriteLine("Cannot recreate pipeline for disposed material or variant!");
				return false;
			}

			if (_dirtyFlags == Material.DirtyFlags.None && pipeline != null && !pipeline.IsDisposed)
			{
				return true;
			}

			try
			{
				lock (lockObj)
				{
					// Purge any outdated previously created pipelines:
					if (pipeline != null && !pipeline.IsDisposed)
					{
						pipeline.Dispose();
					}

					// Update outputs:
					OutputDescription outputs = new();

					// Recreate full description:
					if (_dirtyFlags.HasFlag(Material.DirtyFlags.All))
					{
						pipelineDesc = new(
							BlendStateDescription.SingleOverrideBlend,
							material.GetDepthStencilDesc(),
							material.GetRasterizerStateDesc(),
							PrimitiveTopology.TriangleList,
							material.GetShaderSet(in vertexLayoutDescs),
							material.GetResourceLayouts(),
							outputs,
							ResourceBindingModel.Improved);
					}
					// Update current description:
					else
					{
						if (_dirtyFlags.HasFlag(Material.DirtyFlags.DepthStencil))
						{
							pipelineDesc.DepthStencilState = material.GetDepthStencilDesc();
						}
						if (_dirtyFlags.HasFlag(Material.DirtyFlags.Rasterizer))
						{
							pipelineDesc.RasterizerState = material.GetRasterizerStateDesc();
						}
						if (_dirtyFlags.HasFlag(Material.DirtyFlags.ShaderSet))
						{
							pipelineDesc.ShaderSet = material.GetShaderSet(in vertexLayoutDescs);
						}
						if (_dirtyFlags.HasFlag(Material.DirtyFlags.ResourceLayouts))
						{
							pipelineDesc.ResourceLayouts = material.GetResourceLayouts();
						}
					}

					// create new pipeline resource:
					pipeline = material.graphicsCore.MainFactory.CreateGraphicsPipeline(ref pipelineDesc);
				}
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to create graphics pipeline for variant '{vertexDataFlags}' of material '{material.resourceKey}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				return false;
			}
		}

		private VertexLayoutDescription[] CreateVertexLayouts()
		{
			// Prepare layout and description arrays:
			int resLayoutCount = 1;

			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData)) resLayoutCount++;
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes)) resLayoutCount++;
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations)) resLayoutCount++;

			VertexLayoutDescription[] vertexLayoutDescs = new VertexLayoutDescription[resLayoutCount];

			// Assemble descriptions:
			int i = 0;
			vertexLayoutDescs[i++] = BasicVertex.vertexLayoutDesc;
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData))
			{
				vertexLayoutDescs[i++] = ExtendedVertex.vertexLayoutDesc;
			}
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes))
			{
				vertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;
			}
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations))
			{
				vertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;
			}

			return vertexLayoutDescs;
		}

		#endregion
	}
}
