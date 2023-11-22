using FragEngine3.EngineCore;
using FragEngine3.Graphics.Data;
using FragEngine3.Resources;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	/// <summary>
	/// A graphics resource depicting how a geometry surface shall be rendered. A material is a composite resource made out
	/// of shader programs (for each pipeline stage, and for different vertex variants), bound textures and buffers, as well
	/// as resource layouts and other reusable pipeline definitions that are used for efficiently rendering the surface.<para/>
	/// IMPORT: Resource dependencies and pipeline objects are generated on-demand and just-in-time when they are first used,
	/// if they haven't been loaded in the background beforehand.<para/>
	/// LIFECYCLE: Disposing the material will only release the material resource itself; all shaders and textures referenced
	/// by the material will remain loaded unless they are explicitly disposed themselves - multiple materials and assets may
	/// be referencing a same resource instance as a shared dependency.
	/// </summary>
	public sealed class Material : Resource
	{
		#region Types

		[Flags]
		public enum DirtyFlags : byte
		{
			None = 0x00,

			DepthStencil = 1,
			Rasterizer = 2,
			ShaderSet = 4,
			ResourceLayouts = 8,

			All = DepthStencil | Rasterizer | ShaderSet | ResourceLayouts
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

		private MaterialVariant?[] variants = [];

		private ResourceHandle vertexShader = null!;
		private ResourceHandle? geometryShader = null;
		private ResourceHandle? tesselationShader = null;
		private ResourceHandle pixelShader = null!;

		private ResourceLayout[] resLayouts = [];
		private ResourceLayoutDescription[] resLayoutDescs = [];

		private bool enableDepthRead = true;
		private bool enableDepthWrite = true;
		private bool enableStencil = false;
		private StencilBehaviourDesc stencilBehaviour;
		private bool enableCulling = true;

		private RenderMode renderMode = RenderMode.Opaque;
		private float zSortingBias = 0.0f;

		private DirtyFlags dirtyFlags = DirtyFlags.None;

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

		/// <summary>
		/// Gets whether this material has a valid simplified replacement material assigned.
		/// </summary>
		public bool HasSimplifiedMaterialVersion => SimplifiedMaterialVersion != null && SimplifiedMaterialVersion.IsValid;
		/// <summary>
		/// Gets or sets a replacement material that may be used when simplified or low-detail rendering is required. This may be used for reflections
		/// or distant LODs, or when rendering your game at exceptionally low graphics settings.
		/// </summary>
		public ResourceHandle? SimplifiedMaterialVersion { get; set; } = null;

		/// <summary>
		/// Gets whether this material has a valid shadow map rendering material assigned.
		/// </summary>
		public bool HasShadowMapMaterialVersion => ShadowMapMaterialVersion != null && ShadowMapMaterialVersion.IsValid;
		/// <summary>
		/// Gets or sets a replacement material that may be used when rendering shadow maps for geometry that would otherwise use this material.
		/// </summary>
		public ResourceHandle? ShadowMapMaterialVersion { get; set; } = null;

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

		// RENDERING:

		public RenderMode RenderMode
		{
			get => renderMode;
			set => renderMode = value;
		}
		public float ZSortingBias
		{
			get => zSortingBias;
			set { if (!float.IsNaN(value)) zSortingBias = value; }
		}

		private Logger Logger => graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

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

			if (variants != null)
			{
				foreach (MaterialVariant? variant in variants)
				{
					variant?.Dispose();
				}
			}
			foreach (ResourceLayout layout in resLayouts)
			{
				if (layout != null && !layout.IsDisposed) layout.Dispose();
			}

			if (_disposing)
			{
				variants = [];
				resLayouts = [];
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
		/// <param name="_vertexDataFlags">Flags for which vertex data must be bound to the pipeline.<para/>
		/// NOTE: At least '<see cref="MeshVertexDataFlags.BasicSurfaceData"/>' must be set, all others are optional.</param>
		/// <returns>True if the pipeline could be retrieved or updated successfully, false otherwise.</returns>
		public bool GetOrUpdatePipeline(out Pipeline _outPipeline, MeshVertexDataFlags _vertexDataFlags = MeshVertexDataFlags.BasicSurfaceData)
		{
			if (!_vertexDataFlags.HasFlag(MeshVertexDataFlags.BasicSurfaceData))
			{
				Logger.LogError($"Material's vertex data flags must include at least '{MeshVertexDataFlags.BasicSurfaceData}'!");
				_outPipeline = null!;
				return false;
			}

			// Variants are stored in an array, with as many elements as there are vertex flag permutations:
			if (variants == null || variants.Length < (int)_vertexDataFlags)
			{
				MaterialVariant?[]? oldVariants = variants;
				variants = new MaterialVariant?[(int)MeshVertexDataFlags.ALL];
				oldVariants?.CopyTo(variants, 0);
			}

			// Get or create, then update pipeline for the requested variant:
			int variantIdx = (int)_vertexDataFlags - 1;
			MaterialVariant? variant = variants[variantIdx];

			if (variant == null || variant.IsDisposed)
			{
				variant = new MaterialVariant(this, _vertexDataFlags);
				variants[variantIdx] = variant;
			}
			else if (IsDirty && !variant.UpdatePipeline(dirtyFlags))
			{
				_outPipeline = null!;
				return false;
			}

			// Reset all dirty flags:
			dirtyFlags = DirtyFlags.None;

			// Output up-to-date pipeline and return success:
			_outPipeline = variant.Pipeline;
			return !variant.IsDisposed && !_outPipeline.IsDisposed;
		}

		internal DepthStencilStateDescription GetDepthStencilDesc()
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

		internal RasterizerStateDescription GetRasterizerStateDesc()
		{
			return enableCulling
				? RasterizerStateDescription.Default
				: RasterizerStateDescription.CullNone;
		}

		internal ResourceLayout[] GetResourceLayouts()
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
						resLayout?.Dispose();
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
				Logger.Instance?.LogError("Cannot create material from null or invalid resource handle!");
				_outMaterial = null;
				return false;
			}
			if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot create material using null or disposed resource manager!");
				_outMaterial = null;
				return false;
			}
			if (_graphicsCore == null || !_graphicsCore.IsInitialized)
			{
				_handle.resourceManager.engine.Logger.LogError("Cannot create material using null or uninitialized graphics core!");
				_outMaterial = null;
				return false;
			}

			// Don't do anything if the resource has already been loaded:
			if (_handle.IsLoaded)
			{
				_outMaterial = _handle.GetResource(false, false) as Material;
				return true;
			}

			Logger logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

			// Retrieve the file that this resource is loaded from:
			if (!_handle.resourceManager.GetFileWithResource(_handle.Key, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				logger.LogError($"Could not find source file for resource handle '{_handle}'!");
				_outMaterial = null;
				return false;
			}

			// Try reading raw byte data from file:
			if (!fileHandle.TryReadResourceBytes(_handle, out byte[] bytes))
			{
				logger.LogError($"Failed to read material JSON for resource '{_handle}'!");
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
				logger.LogException($"Failed to decode JSON for resource '{_handle}'!", ex);
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
				logger.LogError($"Material data for resource '{_handle}' is incomplete or invalid!");
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
