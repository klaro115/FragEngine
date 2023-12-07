using FragEngine3.EngineCore;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	[Obsolete("replace this")]
    public class MaterialVariant : IDisposable
	{
		#region Constructors

		public MaterialVariant(MaterialOld _material, MeshVertexDataFlags _vertexDataFlags)
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

		public readonly MaterialOld material;
		public readonly MeshVertexDataFlags vertexDataFlags;
		private readonly VertexLayoutDescription[] vertexLayoutDescs;

		private Shader[] shaders = [];
		private Pipeline pipeline = null!;

		private GraphicsPipelineDescription pipelineDesc;
		//...

		private readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public Pipeline Pipeline => pipeline;

		private Logger Logger => material.graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

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

			if (_disposing)
			{
				shaders = [];
			}
		}

		public bool UpdatePipeline(CameraContext _ctx, MaterialOld.DirtyFlags _dirtyFlags)
		{
			if (IsDisposed || material.IsDisposed)
			{
				Logger.LogError("Cannot recreate pipeline for disposed material or variant!");
				return false;
			}

			if (_dirtyFlags == MaterialOld.DirtyFlags.None && pipeline != null && !pipeline.IsDisposed)
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

					// Recreate full description:
					if (_dirtyFlags.HasFlag(MaterialOld.DirtyFlags.All))
					{
						pipelineDesc = new(
							BlendStateDescription.SingleAlphaBlend,
							material.GetDepthStencilDesc(),
							material.GetRasterizerStateDesc(),
							PrimitiveTopology.TriangleList,
							GetShaderSet(in vertexLayoutDescs, vertexDataFlags),
							material.GetResourceLayouts(),
							_ctx.outputDesc,
							ResourceBindingModel.Improved);
					}
					// Update current description:
					else
					{
						if (_dirtyFlags.HasFlag(MaterialOld.DirtyFlags.DepthStencil))
						{
							pipelineDesc.DepthStencilState = material.GetDepthStencilDesc();
						}
						if (_dirtyFlags.HasFlag(MaterialOld.DirtyFlags.Rasterizer))
						{
							pipelineDesc.RasterizerState = material.GetRasterizerStateDesc();
						}
						if (_dirtyFlags.HasFlag(MaterialOld.DirtyFlags.ShaderSet) || shaders == null || shaders.Length == 0)
						{
							pipelineDesc.ShaderSet = GetShaderSet(in vertexLayoutDescs, vertexDataFlags);
						}
						if (_dirtyFlags.HasFlag(MaterialOld.DirtyFlags.ResourceLayouts))
						{
							pipelineDesc.ResourceLayouts = material.GetResourceLayouts();
						}
						if (_dirtyFlags.HasFlag(MaterialOld.DirtyFlags.Output))
						{
							pipelineDesc.Outputs = _ctx.outputDesc;
						}
					}

					// create new pipeline resource:
					pipeline = material.graphicsCore.MainFactory.CreateGraphicsPipeline(ref pipelineDesc);
					pipeline.Name = $"{material.resourceKey}_{(uint)vertexDataFlags}";
				}
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create graphics pipeline for variant '{vertexDataFlags}' of material '{material.resourceKey}'!", ex);
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

			VertexLayoutDescription[] newVertexLayoutDescs = new VertexLayoutDescription[resLayoutCount];

			// Assemble descriptions:
			int i = 0;
			newVertexLayoutDescs[i++] = BasicVertex.vertexLayoutDesc;
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData))
			{
				newVertexLayoutDescs[i++] = ExtendedVertex.vertexLayoutDesc;
			}
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes))
			{
				newVertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;
			}
			if (vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations))
			{
				newVertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;
			}

			return newVertexLayoutDescs;
		}

		private ShaderSetDescription GetShaderSet(in VertexLayoutDescription[] vertexLayoutDescs, MeshVertexDataFlags _variantFlags)
		{
			// Determine the number of (supported) shader stages:
			bool hasGeometry = material.GeometryShader != null && material.graphicsCore.GetCapabilities().geometryShaders;
			bool hasTesselation = material.TesselationShaderCtrl != null && material.TesselationShaderEval != null && material.graphicsCore.GetCapabilities().tesselationShaders;

			int shaderCount = 2;
			if (hasGeometry) shaderCount++;
			if (hasTesselation) shaderCount++;

			// Populate shader stages:
			int i = 0;
			shaders = new Shader[shaderCount];
			shaders[i++] = GetShader(material.VertexShader);
			if (hasGeometry)
			{
				shaders[i++] = GetShader(material.GeometryShader!);
			}
			if (hasTesselation)
			{
				shaders[i++] = GetShader(material.TesselationShaderCtrl!);
				shaders[i++] = GetShader(material.TesselationShaderEval!);
			}
			shaders[i++] = GetShader(material.PixelShader);

			// Assemble shader set description:
			return new ShaderSetDescription(
				vertexLayoutDescs,
				shaders);


			// Local helper method for loading shader programs from resource handle:
			Shader GetShader(ResourceHandle _handle)
			{
				if (_handle != null &&
					_handle.GetResource(true, true) is ShaderResource res &&
					res.GetShaderProgram(_variantFlags, out Shader? shader))
				{
					return shader!;
				}
				return null!;
			}
		}

		#endregion
	}
}
