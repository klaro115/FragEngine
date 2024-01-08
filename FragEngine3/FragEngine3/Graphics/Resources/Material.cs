using FragEngine3.Containers;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public class Material : Resource
	{
		#region Types

		public struct StencilBehaviourDesc
		{
			public byte readMask;
			public byte writeMask;
			public uint referenceValue;

			public StencilBehaviorDescription stencilFront;
			public StencilBehaviorDescription stencilBack;
		}

		private struct DepthStencilDesc
		{
			public bool enableDepthRead;
			public bool enableDepthWrite;
			public bool enableStencil;
			public StencilBehaviourDesc stencilBehaviour;
			public bool enableCulling;

			public static DepthStencilDesc Default => new()
			{
				enableDepthRead = true,
				enableDepthWrite = true,
				enableStencil = false,
				stencilBehaviour = default,
				enableCulling = true,
			};
		}

		private struct RenderModeDesc
		{
			public RenderMode renderMode;
			public float zSortingBias;
			public bool castShadows;

			public static RenderModeDesc Default => new()
			{
				renderMode = RenderMode.Opaque,
				zSortingBias = 0.0f,
				castShadows = true,
			};
		}

		#endregion
		#region Constructors

		public Material(GraphicsCore _core, ResourceHandle _handle) : base(_handle)
		{
			core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

			CreateDefaultResourceLayout(0);
		}

		#endregion
		#region Fields

		public GraphicsCore core;

		private uint materialVersion = 1000;

		private ResourceHandle vertexShader = ResourceHandle.None;
		private ResourceHandle? geometryShader = null;
		private ResourceHandle? tesselationShaderCtrl = null;
		private ResourceHandle? tesselationShaderEval = null;
		private ResourceHandle pixelShader = ResourceHandle.None;

		private VersionedMember<ResourceLayout> defaultResourceLayout = new(null!, 0);
		private VersionedMember<ShaderSetDescription> shaderSetDesc = new(default, 0);

		private ResourceLayout? boundResourceLayout = null;
		private Tuple<string, int>[]? boundResourceKeys = null;
		private VersionedMember<ResourceSet?> boundResourceSet = new(null, 0);

		private VersionedMember<DepthStencilDesc> depthStencilDesc = new(DepthStencilDesc.Default, 0);
		private VersionedMember<RenderModeDesc> renderModeDesc = new(RenderModeDesc.Default, 0);

		#endregion
		#region Properties

		public override ResourceType ResourceType => ResourceType.Material;

		public bool UseExternalBoundResources { get; private set; } = false;
		public ResourceLayout ResourceLayout => defaultResourceLayout.Value;
		public ResourceLayout? BoundResourceLayout => boundResourceLayout;
		public ResourceSet? BoundResourceSet => boundResourceSet.Value;

		// RENDERING:

		public RenderMode RenderMode
		{
			get => renderModeDesc.Value.renderMode;
			set { RenderModeDesc rmd = renderModeDesc.Value; rmd.renderMode = value; renderModeDesc.UpdateValue(renderModeDesc.Version + 1, rmd); }
		}
		public float ZSortingBias
		{
			get => renderModeDesc.Value.zSortingBias;
			set { if (!float.IsNaN(value)) { RenderModeDesc rmd = renderModeDesc.Value; rmd.zSortingBias = value; renderModeDesc.UpdateValue(renderModeDesc.Version + 1, rmd); } }
		}

		// REPLACEMENTS:

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

		private Logger Logger => core.graphicsSystem.engine.Logger;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			base.Dispose(_disposing);

			defaultResourceLayout.DisposeValue();
			boundResourceLayout?.Dispose();
			boundResourceSet.DisposeValue();
		}

		internal bool IsPipelineUpToDate(in VersionedMember<Pipeline> _pipeline, uint _rendererVersion)
		{
			if (_pipeline.Value == null || _pipeline.Value.IsDisposed)
			{
				return false;
			}
			uint newestPipelineVersion = materialVersion ^ _rendererVersion;
			return newestPipelineVersion == _pipeline.Version;
		}

		private bool CreateDefaultResourceLayout(uint _newVersion)
		{
			try
			{
				defaultResourceLayout.DisposeValue();

				ResourceLayoutDescription resLayoutDesc = new(GraphicsContants.DEFAULT_SURFACE_RESOURCE_LAYOUT_DESC);

				ResourceLayout resLayout = core.MainFactory.CreateResourceLayout(ref resLayoutDesc);
				resLayout.Name = $"ResLayout_Default_{resourceKey}";

				defaultResourceLayout.UpdateValue(_newVersion, resLayout);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create resource layout for material '{resourceKey}'!", ex);
				defaultResourceLayout.DisposeValue();
				return false;
			}
		}

		private bool CreateShaderSetDesc(uint _newVersion, MeshVertexDataFlags _vertexDataFlags)
		{
			shaderSetDesc.DisposeValue();

			GraphicsCapabilities capabilities = core.GetCapabilities();

			// VERTEX DEFINITIONS:

			bool hasExtendedData = _vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
			bool hasBlendShapes = _vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
			bool hasBoneAnimations = _vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations);

			int vertexBufferCount = 1;
			if (hasExtendedData)	vertexBufferCount++;
			if (hasBlendShapes)		vertexBufferCount++;
			if (hasBoneAnimations)	vertexBufferCount++;

			VertexLayoutDescription[] vertexLayoutDescs = new VertexLayoutDescription[vertexBufferCount];

			int i = 0;
			vertexLayoutDescs[i++] = BasicVertex.vertexLayoutDesc;
			if (hasExtendedData)	vertexLayoutDescs[i++] = ExtendedVertex.vertexLayoutDesc;
			if (hasBlendShapes)		vertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;
			if (hasBoneAnimations)	vertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;

			// SHADER SET:

			bool hasGeometryShader = capabilities.geometryShaders && geometryShader != null;
			bool hasTesselationShader = capabilities.tesselationShaders && tesselationShaderCtrl != null && tesselationShaderEval != null;

			int shaderCount = 0;
			if (vertexShader != null)	shaderCount++;
			if (hasGeometryShader)		shaderCount++;
			if (hasTesselationShader)	shaderCount += 2;
			if (pixelShader != null)	shaderCount++;

			try
			{
				Shader[] shaders = new Shader[shaderCount];
				i = 0;
				ShaderStages errorStages = 0;

				bool success = AddShaderVariant(vertexShader, ShaderStages.Vertex);
				if (hasGeometryShader)
				{
					success &= AddShaderVariant(geometryShader, ShaderStages.Geometry);
				}
				if (hasTesselationShader)
				{
					success &= AddShaderVariant(tesselationShaderCtrl, ShaderStages.TessellationControl);
					success &= AddShaderVariant(tesselationShaderEval, ShaderStages.TessellationEvaluation);
				}
				success &= AddShaderVariant(pixelShader, ShaderStages.Fragment);

				if (!success || errorStages != 0)
				{
					Logger.LogError($"One or more shader programs could not be loaded for material '{resourceKey}', variant '{_vertexDataFlags}'. Error stages: '{errorStages}'");
					return false;
				}

				// Assemble shader set:
				ShaderSetDescription ssd = new(
					vertexLayoutDescs,
					shaders);

				shaderSetDesc.UpdateValue(_newVersion, ssd);
				return success;


				// Local helper method for fetching and loading a shader variant:
				bool AddShaderVariant(ResourceHandle? _handle, ShaderStages _stageFlag)
				{
					if (_handle == null || !_handle.IsValid)
					{
						errorStages |= _stageFlag;
						return false;
					}

					if (_handle.GetResource(true, true) is not ShaderResource shaderRes ||
						!shaderRes.GetShaderProgram(_vertexDataFlags, out Shader? shader) ||
						shader == null)
					{
						errorStages |= _stageFlag;
						return false;
					}

					shaders[i++] = shader;
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create shader set description for material '{resourceKey}' and variant '{_vertexDataFlags}'!", ex);
				shaderSetDesc.DisposeValue();
				return false;
			}
		}

		private bool CreateBoundResourceSet()
		{
			boundResourceSet.DisposeValue();
			if (boundResourceLayout == null || boundResourceKeys == null)
			{
				return false;
			}

			BindableResource[] boundResources = new BindableResource[boundResourceKeys.Length];
			for (int i = 0; i < boundResourceKeys.Length; ++i)
			{
				Tuple<string, int> resourceKeys = boundResourceKeys[i];
				if (string.IsNullOrEmpty(resourceKeys.Item1) || !resourceManager.GetResource(resourceKeys.Item1, out ResourceHandle handle))
				{
					return false;
				}

				Resource? resource = handle.GetResource(true, true);
				if (resource is TextureResource texture)
				{
					boundResources[resourceKeys.Item2] = texture.Texture!;
				}
				else
				{
					boundResources[resourceKeys.Item2] = null!;
				}
			}

			try
			{
				ResourceSetDescription resourceSetDesc = new(boundResourceLayout, boundResources);
				ResourceSet resourceSet = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
				resourceSet.Name = $"ResSet_Bound_{resourceKey}";

				boundResourceSet.UpdateValue(materialVersion, resourceSet);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create resource layout for material's bound textures and buffers!", ex);
				return false;
			}
		}

		internal bool CreatePipeline(CameraContext _cameraCtx, uint _rendererVersion, MeshVertexDataFlags _vertexDataFlags, out VersionedMember<Pipeline> _outPipeline)
		{
			if (_cameraCtx == null || !_cameraCtx.IsValid)
			{
				_outPipeline = new(null!, 0);
				return false;
			}

			// Update material version from versioned members:
			{
				uint newestVersion = materialVersion;

				newestVersion = Math.Max(newestVersion, depthStencilDesc.Version);
				newestVersion = Math.Max(newestVersion, defaultResourceLayout.Version);
				newestVersion = Math.Max(newestVersion, shaderSetDesc.Version);
				newestVersion = Math.Max(newestVersion, boundResourceSet.Version);

				if (materialVersion != newestVersion)
				{
					materialVersion = newestVersion + 1;
				}
			}

			// Check and recreate all out-of-date members:
			if (depthStencilDesc.Version != materialVersion)
			{
				depthStencilDesc.UpdateValue(materialVersion, depthStencilDesc.Value);
			}
			if (defaultResourceLayout.Version != materialVersion && !CreateDefaultResourceLayout(materialVersion))
			{
				_outPipeline = new(null!, 0);
				return false;
			}
			if (shaderSetDesc.Version != materialVersion && !CreateShaderSetDesc(materialVersion, _vertexDataFlags))
			{
				_outPipeline = new(null!, 0);
				return false;
			}
			if (boundResourceSet.Value == null && boundResourceLayout != null && !UseExternalBoundResources && !CreateBoundResourceSet())
			{
				_outPipeline = new(null!, 0);
				return false;
			}

			// Try creating the pipeline:
			try
			{
				DepthStencilDesc dsd = depthStencilDesc.Value;
				StencilBehaviourDesc sd = depthStencilDesc.Value.stencilBehaviour;

				DepthStencilStateDescription depthStateDesc = new(
						dsd.enableDepthRead,
						dsd.enableDepthWrite,
						ComparisonKind.LessEqual,
						dsd.enableStencil,
						sd.stencilFront,
						sd.stencilBack,
						sd.readMask,
						sd.writeMask,
						sd.referenceValue);

				RasterizerStateDescription rasterizerState;
				if (dsd.enableCulling)
				{
					rasterizerState = RasterizerStateDescription.Default;
					if (_cameraCtx.mirrorY)
					{
						rasterizerState.FrontFace = FrontFace.CounterClockwise;
					}
				}
				else
				{
					rasterizerState = RasterizerStateDescription.CullNone;
				}

				ResourceLayout[] resourceLayouts = boundResourceLayout != null
					? [ defaultResourceLayout.Value, boundResourceLayout ]
					: [ defaultResourceLayout.Value ];

				GraphicsPipelineDescription pipelineDesc = new(
					BlendStateDescription.SingleAlphaBlend,
					depthStateDesc,
					rasterizerState,
					PrimitiveTopology.TriangleList,
					shaderSetDesc.Value,
					resourceLayouts,
					_cameraCtx.outputDesc);

				Pipeline pipeline = core.MainFactory.CreateGraphicsPipeline(ref pipelineDesc);
				uint newPipelineVersion = materialVersion ^ _rendererVersion;

				PixelFormat colorFormat = _cameraCtx.outputDesc.ColorAttachments[0].Format;
				pipeline.Name = $"{resourceKey}_{colorFormat}";

				_outPipeline = new(pipeline, newPipelineVersion);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create pipeline for material '{resourceKey}' and variant '{_vertexDataFlags}'!", ex);
				_outPipeline = new(null!, 0);
				return false;
			}
		}

		public override IEnumerator<ResourceHandle> GetResourceDependencies()
		{
			// Shader programs:
			if (vertexShader != null)	yield return vertexShader;
			if (geometryShader != null)	yield return geometryShader;
			if (tesselationShaderCtrl != null && tesselationShaderEval != null)
			{
				yield return tesselationShaderCtrl;
				yield return tesselationShaderEval;
			}
			if (pixelShader != null)	yield return pixelShader;

			//TODO: Iterate dependencies and referenced assets.

			// Lastly, return this material itself:
			if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
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
			if (!_handle.resourceManager.GetFileWithResource(_handle.resourceKey, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				logger.LogError($"Could not find source file for resource handle '{_handle}'!");
				_outMaterial = null;
				return false;
			}

			// Try reading raw byte data from file:
			if (!fileHandle.TryReadResourceBytes(_graphicsCore.graphicsSystem, _handle, out byte[] bytes, out int byteCount))
			{
				logger.LogError($"Failed to read material JSON for resource '{_handle}'!");
				_outMaterial = null;
				return false;
			}

			// Try converting byte data to string containing JSON-encoded material data:
			string jsonTxt;
			try
			{
				jsonTxt = System.Text.Encoding.UTF8.GetString(bytes, 0, byteCount);
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

			// Assemble layout descriptions for bound resources:
			ResourceLayout? boundResourceLayout = null;
			if (data.GetBoundResourceLayoutDesc(out ResourceLayoutDescription boundResourceLayoutDesc, out Tuple<string, int>[]? boundResourceKeys, out bool useExternalBoundResources))
			{
				boundResourceLayout = _graphicsCore.MainFactory.CreateResourceLayout(boundResourceLayoutDesc);
				boundResourceLayout.Name = $"ResLayout_Bound_{_handle.resourceKey}";
			}

			// Assemble stencil description, if required and available:
			StencilBehaviourDesc stencilDesc;
			if (data.States.StencilFront != null && data.States.StencilBack != null)
			{
				stencilDesc = new()
				{
					stencilFront = new(
						data.States.StencilFront.Fail,
						data.States.StencilFront.Pass,
						data.States.StencilFront.DepthFail,
						data.States.StencilFront.ComparisonKind),
					stencilBack = new(
						data.States.StencilBack.Fail,
						data.States.StencilBack.Pass,
						data.States.StencilBack.DepthFail,
						data.States.StencilBack.ComparisonKind),
					readMask = data.States.StencilReadMask,
					writeMask = data.States.StencilWriteMask,
					referenceValue = data.States.StencilReferenceValue,
				};
			}
			else
			{
				stencilDesc = new();
			}

			// Create and initialize material instance from data.
			_outMaterial = new(_graphicsCore, _handle)
			{
				materialVersion = 1,

				vertexShader = GetResourceHandle(data.Shaders.Vertex) ?? ResourceHandle.None,
				geometryShader = GetResourceHandle(data.Shaders.Geometry),
				tesselationShaderCtrl = GetResourceHandle(data.Shaders.TesselationCtrl),
				tesselationShaderEval = GetResourceHandle(data.Shaders.TesselationEval),
				pixelShader = GetResourceHandle(data.Shaders.Pixel) ?? ResourceHandle.None,

				depthStencilDesc = new(new()
				{
					enableDepthRead = data.States!.EnableDepthTest,
					enableDepthWrite = data.States.EnableDepthWrite,
					enableStencil = data.States.EnableStencil,
					stencilBehaviour = stencilDesc,
					enableCulling = data.States.EnableCulling,
				}, 0),

				renderModeDesc = new(new()
				{
					renderMode = data.States.RenderMode,
					zSortingBias = data.States.ZSortingBias,
					castShadows = data.States.CastShadows,
				}, 0),

				UseExternalBoundResources = useExternalBoundResources,
				boundResourceLayout = boundResourceLayout,
				boundResourceKeys = boundResourceKeys,
				boundResourceSet = new(null!, 0),

				SimplifiedMaterialVersion = GetResourceHandle(data.Replacements?.SimplifiedVersion),
				ShadowMapMaterialVersion = GetResourceHandle(data.Replacements?.ShadowMap),
			};
			return true;


			// Local helper methods for getching shader handles:
			ResourceHandle? GetResourceHandle(string? _resourceKey)
			{
				return !string.IsNullOrEmpty(_resourceKey) && _handle.resourceManager.GetResource(_resourceKey, out ResourceHandle handle)
					? handle
					: null;
			}
		}

		public bool CreateMaterialData(out MaterialData _outData)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot create material data for disposed material resource!");
				_outData = null!;
				return false;
			}

			DepthStencilDesc dsd = depthStencilDesc.Value;
			RenderModeDesc rmd = renderModeDesc.Value;

			// Gather and assemble material data:
			_outData = new()
			{
				Key = resourceKey,

				States = new()
				{
					EnableDepthTest = dsd.enableDepthRead,
					EnableDepthWrite = dsd.enableDepthWrite,

					EnableStencil = dsd.enableStencil,
					StencilFront = new()
					{
						Fail = dsd.stencilBehaviour.stencilFront.Fail,
						Pass = dsd.stencilBehaviour.stencilFront.Pass,
						DepthFail = dsd.stencilBehaviour.stencilFront.DepthFail,
						ComparisonKind = dsd.stencilBehaviour.stencilFront.Comparison,
					},
					StencilBack = new()
					{
						Fail = dsd.stencilBehaviour.stencilBack.Fail,
						Pass = dsd.stencilBehaviour.stencilBack.Pass,
						DepthFail = dsd.stencilBehaviour.stencilBack.DepthFail,
						ComparisonKind = dsd.stencilBehaviour.stencilBack.Comparison,
					},
					StencilReadMask = dsd.stencilBehaviour.readMask,
					StencilWriteMask = dsd.stencilBehaviour.writeMask,
					StencilReferenceValue = dsd.stencilBehaviour.referenceValue,

					EnableCulling = dsd.enableCulling,

					RenderMode = rmd.renderMode,
					ZSortingBias = rmd.zSortingBias,
					CastShadows = rmd.castShadows,
				},

				Shaders = new()
				{
					IsSurfaceMaterial = true,
					Compute = string.Empty,
					Vertex = vertexShader.resourceKey,
					Geometry = geometryShader?.resourceKey ?? string.Empty,
					TesselationCtrl = tesselationShaderCtrl?.resourceKey ?? string.Empty,
					TesselationEval = tesselationShaderEval?.resourceKey ?? string.Empty,
					Pixel = pixelShader.resourceKey,
				},

				Replacements = new()
				{
					SimplifiedVersion = SimplifiedMaterialVersion?.resourceKey ?? string.Empty,
					ShadowMap = ShadowMapMaterialVersion?.resourceKey ?? string.Empty,
				},

				Resources = new()
				{
					// TODO
				},
			};
			return _outData.IsValid();
		}

		#endregion
	}
}
