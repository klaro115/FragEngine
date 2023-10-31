using FragEngine3.Graphics.Data;
using FragEngine3.Resources;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public sealed class Material : Resource
	{
		#region Types

		[Flags]
		private enum DirtyFlags : byte
		{
			None				= 0x00,

			DepthStencil		= 1,
			Rasterizer			= 2,
			ShaderSet			= 4,
			ResourceLayouts		= 8,

			All					= DepthStencil | Rasterizer | ShaderSet | ResourceLayouts
		}

		public struct StencilBehaviourDesc
		{
			public byte readMask;
			public byte writeMask;
			public uint referenceValue;

			public StencilBehaviorDescription stencilFront;
			public StencilBehaviorDescription stencilBack;
		}

		#endregion
		#region Constructors

		public Material(ResourceHandle _handle, GraphicsCore _graphicsCore) : base(_handle)
		{
			graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Material's graphics core may not be null!");
		}

		#endregion
		#region Fields

		public readonly GraphicsCore graphicsCore;

		private Pipeline? pipeline = null;

		private ResourceHandle vertexShader = null!;
		private ResourceHandle? geometryShader = null;
		private ResourceHandle? tesselationShader = null;
		private ResourceHandle pixelShader = null!;

		private GraphicsPipelineDescription pipelineDesc;
		private ResourceLayoutDescription[] resLayoutDescs = Array.Empty<ResourceLayoutDescription>();
		private ResourceLayout[] resLayouts = Array.Empty<ResourceLayout>();
		private Shader[] shaders = Array.Empty<Shader>();

		private bool enableDepthRead = true;
		private bool enableDepthWrite = true;
		private bool enableStencil = false;
		private StencilBehaviourDesc stencilBehaviour;
		private bool enableCulling = true;

		private DirtyFlags dirtyFlags = DirtyFlags.None;

		private readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsDirty => dirtyFlags != 0;

		public override ResourceType ResourceType => ResourceType.Material;

		// SHADERS:

		public ResourceHandle VertexShader
		{
			get => vertexShader;
			set { bool isChanged = vertexShader.Key != value?.Key; vertexShader = value!; if (isChanged) { MarkDirty(DirtyFlags.ShaderSet); } }
		}
		public ResourceHandle? GeometryShader
		{
			get => geometryShader;
			set { bool isChanged = geometryShader?.Key != value?.Key; geometryShader = value!; if (isChanged) { MarkDirty(DirtyFlags.ShaderSet); } }
		}
		public ResourceHandle? TesselationShader
		{
			get => tesselationShader;
			set { bool isChanged = tesselationShader?.Key != value?.Key; tesselationShader = value!; if (isChanged) { MarkDirty(DirtyFlags.ShaderSet); } }
		}
		public ResourceHandle PixelShader
		{
			get => pixelShader;
			set { bool isChanged = pixelShader.Key != value?.Key; pixelShader = value!; if (isChanged) { MarkDirty(DirtyFlags.ShaderSet); } }
		}

		// STATES:

		public bool EnableDepthRead
		{
			get => enableDepthRead;
			set { bool isChanged = enableDepthRead != value; enableDepthRead = value; if (isChanged) { MarkDirty(DirtyFlags.DepthStencil); } }
		}
		public bool EnableDepthWrite
		{
			get => enableDepthWrite;
			set { bool isChanged = enableDepthWrite != value; enableDepthWrite = value; if (isChanged) { MarkDirty(DirtyFlags.DepthStencil); } }
		}
		public bool EnableStencil
		{
			get => enableStencil;
			set { bool isChanged = enableStencil != value; enableStencil = value; if (isChanged) { MarkDirty(DirtyFlags.DepthStencil); } }
		}
		public StencilBehaviourDesc StencilBehaviour
		{
			get => stencilBehaviour;
			set { stencilBehaviour = value; MarkDirty(DirtyFlags.DepthStencil); }
		}
		public bool EnableCulling
		{
			get => enableCulling;
			set { bool isChanged = enableCulling != value; enableCulling = value; if (isChanged) { MarkDirty(DirtyFlags.Rasterizer); } }
		}

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsDisposed = true;

			if (_disposing)
			{
				vertexShader = null!;
				geometryShader = null;
				tesselationShader = null;
				pixelShader = null!;

				//TODO: Drop bound resources.
			}

			foreach (ResourceLayout layout in resLayouts)
			{
				if (!layout.IsDisposed) layout.Dispose();
			}

			if (_disposing)
			{
				resLayouts = Array.Empty<ResourceLayout>();
				shaders = Array.Empty<Shader>();
			}
		}

		public void MarkDirty()
		{
			dirtyFlags = DirtyFlags.All;
		}
		private void MarkDirty(DirtyFlags _flags)
		{
			dirtyFlags |= _flags;
		}

		/// <summary>
		/// Gets the material's graphics pipeline, or recreates it if outdated.<para/>
		/// NOTE: The pipeline must be rebuilt each time the material is marked as dirty via '<see cref="MarkDirty()"/>',
		/// which is done automatically if any of its flags, shaders, or bound resources have changed. In any other case,
		/// the previous pipeline should still be valid and is returned instead.
		/// </summary>
		/// <param name="_outPipeline">Outputs the graphics pipeline encoding how to render stuff with this material.</param>
		/// <returns>True if the pipeline could be retrieved or updated successfully, false otherwise.</returns>
		public bool GetOrUpdatePipeline(out Pipeline _outPipeline)
		{
			if (IsDirty || pipeline == null)
			{
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
						if (dirtyFlags.HasFlag(DirtyFlags.All))
						{
							pipelineDesc = new(
								BlendStateDescription.SingleOverrideBlend,
								GetDepthStencilDesc(),
								GetRasterizerStateDesc(),
								PrimitiveTopology.TriangleList,
								GetShaderSet(),
								resLayouts,
								outputs,
								ResourceBindingModel.Improved);
						}
						// Update current description:
						else
						{
							if (dirtyFlags.HasFlag(DirtyFlags.DepthStencil))
							{
								pipelineDesc.DepthStencilState = GetDepthStencilDesc();
							}
							if (dirtyFlags.HasFlag(DirtyFlags.Rasterizer))
							{
								pipelineDesc.RasterizerState = GetRasterizerStateDesc();
							}
							if (dirtyFlags.HasFlag(DirtyFlags.ShaderSet))
							{
								pipelineDesc.ShaderSet = GetShaderSet();
							}
							if (dirtyFlags.HasFlag(DirtyFlags.ResourceLayouts))
							{
								pipelineDesc.ResourceLayouts = GetResourceLayouts();
							}
						}

						// create new pipeline resource:
						pipeline = graphicsCore.MainFactory.CreateGraphicsPipeline(ref pipelineDesc);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error! Failed to create graphics pipeline for material '{resourceKey}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
					_outPipeline = null!;
					return false;
				}
			}

			// Reset all dirty flags:
			dirtyFlags = DirtyFlags.None;

			// Output up-to-date pipeline and return success:
			_outPipeline = pipeline;
			return true;
		}

		private DepthStencilStateDescription GetDepthStencilDesc()
		{
			if (enableStencil)
			{
				return new(
					enableDepthRead,
					enableDepthWrite,
					ComparisonKind.LessEqual,
					true,
					stencilBehaviour.stencilFront,
					stencilBehaviour.stencilBack,
					stencilBehaviour.readMask,
					stencilBehaviour.writeMask,
					stencilBehaviour.referenceValue);
			}
			else
			{
				return new(
					enableDepthRead,
					enableDepthWrite,
					ComparisonKind.LessEqual);
			}
		}

		private RasterizerStateDescription GetRasterizerStateDesc()
		{
			return enableCulling
				? RasterizerStateDescription.Default
				: RasterizerStateDescription.CullNone;
		}

		private ShaderSetDescription GetShaderSet()
		{
			// Determine the number of (supported) shader stages:
			bool hasGeometry = geometryShader != null && graphicsCore.GetCapabilities().geometryShaders;
			bool hasTesselation = tesselationShader != null && graphicsCore.GetCapabilities().tesselationShaders;

			int shaderCount = 2;
			if (hasGeometry) shaderCount++;
			if (hasTesselation) shaderCount++;

			// Populate shader stages:
			int i = 0;
			shaders = new Shader[shaderCount];
			shaders[i++] = GetShader(vertexShader);
			if (hasGeometry)
			{
				shaders[i++] = GetShader(geometryShader!);
			}
			if (hasTesselation)
			{
				shaders[i++] = GetShader(tesselationShader!);
			}
			shaders[i++] = GetShader(pixelShader);

			// Assemble shader set description:
			return new ShaderSetDescription(
				GraphicsContants.SURFACE_VERTEX_LAYOUT_BASIC,
				shaders);


			// Local helper method for loading shader programs from resource handle:
			static Shader GetShader(ResourceHandle _handle)
			{
				if (_handle != null && _handle.GetResource(true, true) is ShaderResource res)
				{
					return res.Shader;
				}
				return null!;
			}
		}

		private ResourceLayout[] GetResourceLayouts()
		{
			const int resLayoutCount = 1;

			// Assemble descriptions:
			if (resLayoutDescs == null || resLayoutDescs.Length != resLayoutCount)
			{
				resLayoutDescs = new ResourceLayoutDescription[resLayoutCount];
			}
			resLayoutDescs[0] = new ResourceLayoutDescription(GraphicsContants.DEFAULT_SURFACE_RESOURCE_LAYOUT_DESC);

			// Create layouts:
			if (resLayouts == null || resLayouts.Length != resLayoutCount)
			{
				// Dispose outdated existing layouts:
				if (resLayouts != null)
				{
					foreach (ResourceLayout resLayout in resLayouts)
					{
						resLayout.Dispose();
					}
				}

				resLayouts = new ResourceLayout[resLayoutCount];
			}
			for (int i = 0; i < resLayoutCount; ++i)
			{
				resLayouts[i] = graphicsCore.MainFactory.CreateResourceLayout(ref resLayoutDescs[i]);
			}

			return resLayouts;
		}

		public override IEnumerator<ResourceHandle> GetResourceDependencies()
		{
			if (IsDisposed) yield break;

			bool hasGeometry = geometryShader != null && graphicsCore.GetCapabilities().geometryShaders;
			bool hasTesselation = tesselationShader != null && graphicsCore.GetCapabilities().tesselationShaders;

			// Iterate all (supported) shader stages:
			if (vertexShader != null) yield return vertexShader;
			if (hasGeometry) yield return geometryShader!;
			if (hasTesselation) yield return tesselationShader!;
			if (pixelShader != null) yield return pixelShader;

			//TODO: Enumerate bound resources.

			// Lastly, return self:
			if (GetResourceHandle(out ResourceHandle handle))
			{
				yield return handle;
			}
		}

		public static bool CreateMaterial(ResourceHandle _handle, GraphicsCore _graphicsCore, out Material? _outMaterial)
		{
			if (_handle == null || !_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot create material from null or invalid resource handle!");
				_outMaterial = null;
				return false;
			}
			if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
			{
				Console.WriteLine("Error! Cannot create material using null or disposed resource manager!");
				_outMaterial = null;
				return false;
			}
			if (_graphicsCore == null || !_graphicsCore.IsInitialized)
			{
				Console.WriteLine("Error! Cannot create material using null or uninitialized graphics core!");
				_outMaterial = null;
				return false;
			}

			// Don't do anything if the resource has already been loaded:
			if (_handle.IsLoaded)
			{
				_outMaterial = _handle.GetResource(false, false) as Material;
				return true;
			}

			// Retrieve the file that this resource is loaded from:
			if (!_handle.resourceManager.GetFileWithResource(_handle.Key, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				Console.WriteLine($"Error! Could not find source file for resource handle '{_handle}'!");
				_outMaterial = null;
				return false;
			}

			// Try reading raw byte data from file:
			if (!fileHandle.TryReadResourceBytes(_handle, out byte[] bytes))
			{
				Console.WriteLine($"Error! Failed to read material JSON for resource '{_handle}'!");
				_outMaterial = null;
				return false;
			}

			// Try converting byte data to string containing JSON-encoded material data:
			string jsonTxt;
			try
			{
				jsonTxt = System.Text.Encoding.UTF8.GetString(bytes);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to decode JSON for resource '{_handle}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outMaterial = null;
				return false;
			}

			// Try deserializing material description data from JSON:
			if (!Serializer.DeserializeFromJson(jsonTxt, out MaterialData? data) || data == null)
			{
				_outMaterial = null;
				return false;
			}

			// Double-check if the data actually makes any sense:
			if (!data.IsValid())
			{
				Console.WriteLine($"Error! Material data for resource '{_handle}' is incomplete or invalid!");
				_outMaterial = null;
				return false;
			}

			//TODO: Create and initialize material instance from data.

			_outMaterial = null;
			return true;
		}

		#endregion
	}
}
