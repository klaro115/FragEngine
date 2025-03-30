using FragEngine3.Containers;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.MaterialTypes;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

[Obsolete("Rewritten")]
public class MaterialOld(GraphicsCore _core, ResourceHandle _handle) : Resource(_handle)
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

		public static RenderModeDesc Default => new()
		{
			renderMode = RenderMode.Opaque,
			zSortingBias = 0.0f,
		};
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");
	private readonly Logger logger = _core.graphicsSystem.Engine.Logger;

	private uint materialVersion = 1000;

	private ResourceHandle vertexShader = ResourceHandle.None;
	private ResourceHandle? geometryShader = null;
	private ResourceHandle? tesselationShaderCtrl = null;
	private ResourceHandle? tesselationShaderEval = null;
	private ResourceHandle pixelShader = ResourceHandle.None;

	private VersionedMember<ShaderSetDescription>[]? shaderSetDescs = null;

	private ResourceLayout? boundResourceLayout = null;
	private MaterialBoundResourceKeys[]? boundResourceKeys = null;
	private VersionedMember<ResourceSet?> boundResourceSet = new(null, 0);

	private VersionedMember<DepthStencilDesc> depthStencilDesc = new(DepthStencilDesc.Default, 0);
	private VersionedMember<RenderModeDesc> renderModeDesc = new(RenderModeDesc.Default, 0);

	#endregion
	#region Properties

	public override ResourceType ResourceType => ResourceType.Material;

	public bool UseExternalBoundResources { get; private set; } = false;
	public ResourceLayout? BoundResourceLayout => boundResourceLayout;
	public ResourceSet? BoundResourceSet => boundResourceSet.Value;

	// RENDERING:

	public RenderMode RenderMode
	{
		get => renderModeDesc.Value.renderMode;
		set
		{
			RenderModeDesc rmd = renderModeDesc.Value;
			rmd.renderMode = value;
			renderModeDesc.UpdateValue(renderModeDesc.Version + 1, rmd);
		}
	}
	public float ZSortingBias
	{
		get => renderModeDesc.Value.zSortingBias;
		set
		{
			if (!float.IsNaN(value))
			{
				RenderModeDesc rmd = renderModeDesc.Value;
				rmd.zSortingBias = value;
				renderModeDesc.UpdateValue(renderModeDesc.Version + 1, rmd);
			}
		}
	}

	// REPLACEMENTS:

	/// <summary>
	/// Gets whether this material has a valid simplified replacement material assigned.
	/// </summary>
	public bool HasSimplifiedMaterialVersion => SimplifiedMaterialVersion is not null && SimplifiedMaterialVersion.IsValid;
	/// <summary>
	/// Gets or sets a replacement material that may be used when simplified or low-detail rendering is required. This may be used for reflections
	/// or distant LODs, or when rendering your game at exceptionally low graphics settings.
	/// </summary>
	public ResourceHandle? SimplifiedMaterialVersion { get; set; } = null;

	/// <summary>
	/// Gets whether this material has a valid shadow map rendering material assigned.
	/// </summary>
	public bool HasShadowMapMaterialVersion => ShadowMapMaterialVersion is not null && ShadowMapMaterialVersion.IsValid;
	/// <summary>
	/// Gets or sets a replacement material that may be used when rendering shadow maps for geometry that would otherwise use this material.
	/// </summary>
	public ResourceHandle? ShadowMapMaterialVersion { get; set; } = null;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		boundResourceLayout?.Dispose();
		boundResourceSet.DisposeValue();
	}

	internal bool IsPipelineUpToDate(in PipelineState? _pipeline, uint _rendererVersion)
	{
		if (_pipeline is null || _pipeline.IsDisposed)
		{
			return false;
		}
		uint newestPipelineVersion = materialVersion ^ _rendererVersion;
		return newestPipelineVersion == _pipeline.version;
	}

	private void ResizeShaderSetDescsForVariant(uint _variantIdx)
	{
		uint minVariantCount = _variantIdx + 1;

		if (shaderSetDescs is null)
		{
			shaderSetDescs = new VersionedMember<ShaderSetDescription>[minVariantCount];
			Array.Fill(shaderSetDescs, new(default, 0));
		}
		else if (shaderSetDescs.Length < minVariantCount)
		{
			int oldShaderSetDescCount = shaderSetDescs.Length;
			VersionedMember<ShaderSetDescription>[]? oldShaderSetDescs = shaderSetDescs;

			shaderSetDescs = new VersionedMember<ShaderSetDescription>[minVariantCount];

			oldShaderSetDescs?.CopyTo(shaderSetDescs, 0);
			for (int i = oldShaderSetDescCount; i < shaderSetDescs.Length; ++i)
			{
				shaderSetDescs[i] = new(default, 0);
			}
		}
	}

	private bool LoadShaderVariant(ResourceHandle? _handle, ShaderStages _stageFlag, bool _fallbackToBasicVariant, ref MeshVertexDataFlags _vertexDataFlags, out Shader? _outShader)
	{
		if (_handle is null || !_handle.IsValid)
		{
			_outShader = null;
			return false;
		}

		if (_handle.GetResource(true, true) is not ShaderResource shaderRes)
		{
			logger.LogWarning($"Shader resource '{_handle}' could not be found! (Stage: {_stageFlag}");
			_outShader = null;
			return false;
		}
		if (!shaderRes.GetShaderProgram(_vertexDataFlags, out _outShader) || _outShader is null)
		{
			// If allowed, try falling back to basic variant, see if that can be loaded:
			if (!_fallbackToBasicVariant || !shaderRes.GetShaderProgram(MeshVertexDataFlags.BasicSurfaceData, out _outShader) || _outShader is null)
			{
				logger.LogWarning($"Shader variant program '{_vertexDataFlags}' of shader resource '{_handle.resourceKey}' could not be loaded! (Stage: {_stageFlag}");
				_outShader = null!;
				return false;
			}
			// Change vertex flags for all subsequent stages to basic-only:
			_vertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
		}
		return true;
	}

	private bool CreateShaderSetDesc(uint _newVersion, MeshVertexDataFlags _vertexDataFlags)
	{
		uint variantIdx = _vertexDataFlags.GetVariantIndex();

		// Resize array of shader sets to match number of material variants in use:
		ResizeShaderSetDescsForVariant(variantIdx);
		shaderSetDescs![variantIdx].DisposeValue();

		GraphicsCapabilities capabilities = core.GetCapabilities();

		// VERTEX DEFINITIONS:

		bool fallbackToBasicVariant = _vertexDataFlags != MeshVertexDataFlags.BasicSurfaceData;

		bool hasExtendedData = _vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
		bool hasBlendShapes = _vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
		bool hasBoneAnimations = _vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations);

		int vertexBufferCount = 1;
		if (hasExtendedData) vertexBufferCount++;
		if (hasBlendShapes) vertexBufferCount++;
		if (hasBoneAnimations) vertexBufferCount++;

		VertexLayoutDescription[] vertexLayoutDescs = new VertexLayoutDescription[vertexBufferCount];

		int i = 0;
		vertexLayoutDescs[i++] = BasicVertex.vertexLayoutDesc;
		if (hasExtendedData) vertexLayoutDescs[i++] = ExtendedVertex.vertexLayoutDesc;
		if (hasBlendShapes) vertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;
		if (hasBoneAnimations) vertexLayoutDescs[i++] = IndexedWeightedVertex.vertexLayoutDesc;

		// SHADER SET:

		bool hasGeometryShader = capabilities.geometryShaders && geometryShader is not null;
		bool hasTesselationShader = capabilities.tesselationShaders && tesselationShaderCtrl is not null && tesselationShaderEval is not null;

		int shaderCount = 0;
		if (vertexShader is not null) shaderCount++;
		if (hasGeometryShader) shaderCount++;
		if (hasTesselationShader) shaderCount += 2;
		if (pixelShader is not null) shaderCount++;

		try
		{
			Shader[] shaders = new Shader[shaderCount];
			i = 0;
			ShaderStages errorStages = 0;

			bool success = AddShaderVariant(vertexShader, ShaderStages.Vertex, false);
			if (hasGeometryShader)
			{
				success &= AddShaderVariant(geometryShader, ShaderStages.Geometry, false);
			}
			if (hasTesselationShader)
			{
				success &= AddShaderVariant(tesselationShaderCtrl, ShaderStages.TessellationControl, fallbackToBasicVariant);
				success &= AddShaderVariant(tesselationShaderEval, ShaderStages.TessellationEvaluation, fallbackToBasicVariant);
			}
			success &= AddShaderVariant(pixelShader, ShaderStages.Fragment, fallbackToBasicVariant);

			if (!success || errorStages != 0)
			{
				logger.LogError($"One or more shader programs could not be loaded for material '{resourceKey}', variant '{_vertexDataFlags}'. Error stages: '{errorStages}'");
				return false;
			}

			// Assemble shader set:
			ShaderSetDescription ssd = new(
				vertexLayoutDescs,
				shaders);

			shaderSetDescs[variantIdx].UpdateValue(_newVersion, ssd);
			return success;


			// Local helper method for fetching and loading a shader variant:
			bool AddShaderVariant(ResourceHandle? _handle, ShaderStages _stageFlag, bool _fallbackToBasicVariant)
			{
				if (!LoadShaderVariant(_handle, _stageFlag, _fallbackToBasicVariant, ref _vertexDataFlags, out Shader? shader) || shader is null)
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
			logger.LogException($"Failed to create shader set description for material '{resourceKey}' and variant '{_vertexDataFlags}'!", ex);
			shaderSetDescs[variantIdx].DisposeValue();
			return false;
		}
	}

	private bool LoadBoundResource_Sampler(string? _resourceDescription, out Sampler _outSampler)
	{
		if (string.IsNullOrEmpty(_resourceDescription))
		{
			logger.LogError($"Cannot create sampler for bound resources of material '{resourceKey}' using null or empty description!");
			_outSampler = null!;
			return false;
		}

		// Get or create a sampler from the graphics core's centralized sampler registry:
		if (!core.SamplerManager.GetSampler(_resourceDescription, out _outSampler))
		{
			logger.LogError($"Failed to create sampler for bound resources of material '{resourceKey}'!");
			return false;
		}
		return true;
	}

	private bool LoadBoundResource_Texture(in MaterialBoundResourceKeys _resourceKeys, out Texture _outTexture)
	{
		if (string.IsNullOrEmpty(_resourceKeys.resourceKey) || !resourceManager.GetResource(_resourceKeys.resourceKey, out ResourceHandle handle))
		{
			handle = core.graphicsSystem.TexPlaceholderMagenta;
			logger.LogWarning($"Texture resource key '{_resourceKeys.resourceKey}' could not be found; Reverting to placeholder resource in slot {_resourceKeys.slotIdx} of material '{resourceKey}'");
		}
		if (handle.resourceType != ResourceType.Texture)
		{
			handle = core.graphicsSystem.TexPlaceholderMagenta;
			logger.LogWarning($"Resource '{_resourceKeys.resourceKey}' is not a texture; Reverting to placeholder resource in slot {_resourceKeys.slotIdx} of material '{resourceKey}'");
		}

		TextureResource? resource = handle.GetResource(true, true) as TextureResource;
		resource ??= core.graphicsSystem.TexPlaceholderMagenta.GetResource(false, false) as TextureResource;

		_outTexture = resource?.Texture!;
		return _outTexture != null && !_outTexture.IsDisposed;
	}

	private bool CreateBoundResourceSet()
	{
		// Purge outdated previously created resource sets:
		boundResourceSet.DisposeValue();
		if (boundResourceLayout is null || boundResourceKeys is null)
		{
			return false;
		}

		bool success = true;

		// Gather and load all resources that need to be bound:
		BindableResource[] boundResources = new BindableResource[boundResourceKeys.Length];
		for (int i = 0; i < boundResourceKeys.Length; ++i)
		{
			MaterialBoundResourceKeys resourceKeys = boundResourceKeys[i];

			switch (resourceKeys.resourceKind)
			{
				case ResourceKind.StructuredBufferReadOnly:
					{
						logger.LogError($"Structured buffers are not supported yet for use in bound resources! (Material: '{resourceKey}', Slot: {resourceKeys.slotIdx})");
						success = false;
					}
					break;
				case ResourceKind.TextureReadOnly:
					if (success &= LoadBoundResource_Texture(in resourceKeys, out Texture texture))
					{
						boundResources[resourceKeys.resourceIdx] = texture;
					}
					break;
				case ResourceKind.Sampler:
					if (success &= LoadBoundResource_Sampler(resourceKeys.description, out Sampler sampler))
					{
						boundResources[resourceKeys.resourceIdx] = sampler;
					}
					break;
				default:
					success = false;
					logger.LogError($"Unsupported or unknown resource kind '{resourceKeys.resourceKind}' for bound resource slot '{resourceKeys.slotIdx}'!");
					break;
			}
		}
		if (!success)
		{
			logger.LogError($"Failed to load resources or fallbacks for bound resource set of material '{resourceKey}'!");
			return false;
		}

		// Create the resource set:
		try
		{
			ResourceSetDescription resourceSetDesc = new(boundResourceLayout, boundResources);

			ResourceSet resourceSet = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
			resourceSet.Name = $"ResSet_Bound_{resourceKey}_v{materialVersion}";

			boundResourceSet.UpdateValue(materialVersion, resourceSet);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogException("Failed to create resource layout for material's bound textures and buffers!", ex);
			return false;
		}
	}

	internal bool CreatePipeline(SceneContext _sceneCtx, CameraPassContext _cameraPassCtx, uint _rendererVersion, MeshVertexDataFlags _vertexDataFlags, out PipelineState _outPipeline)
	{
		if (_sceneCtx is null || _cameraPassCtx is null)
		{
			_outPipeline = null!;
			return false;
		}

		uint variantIdx = _vertexDataFlags.GetVariantIndex();
		ResizeShaderSetDescsForVariant(variantIdx);
		VersionedMember<ShaderSetDescription> shaderSetDesc = shaderSetDescs![variantIdx];

		// Update material version from versioned members:
		{
			uint newestVersion = materialVersion;

			newestVersion = Math.Max(newestVersion, depthStencilDesc.Version);
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
		if (shaderSetDesc.Version != materialVersion)
		{
			if (!CreateShaderSetDesc(materialVersion, _vertexDataFlags))
			{
				_outPipeline = null!;
				return false;
			}
			shaderSetDesc = shaderSetDescs![variantIdx];
		}
		if (boundResourceSet.Value is null && boundResourceLayout is not null && !UseExternalBoundResources && !CreateBoundResourceSet())
		{
			_outPipeline = null!;
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
				if (_cameraPassCtx.MirrorY)
				{
					rasterizerState.FrontFace = FrontFace.CounterClockwise;
				}
			}
			else
			{
				rasterizerState = RasterizerStateDescription.CullNone;
			}

			ResourceLayout[] resourceLayouts = boundResourceLayout is not null
				? [_sceneCtx.ResLayoutCamera, _sceneCtx.ResLayoutObject, boundResourceLayout]
				: [_sceneCtx.ResLayoutCamera, _sceneCtx.ResLayoutObject];

			GraphicsPipelineDescription pipelineDesc = new(
				BlendStateDescription.SingleAlphaBlend,
				depthStateDesc,
				rasterizerState,
				PrimitiveTopology.TriangleList,
				shaderSetDesc.Value,
				resourceLayouts,
				_cameraPassCtx.OutputDesc);

			Pipeline pipeline = core.MainFactory.CreateGraphicsPipeline(ref pipelineDesc);
			uint newPipelineVersion = materialVersion ^ _rendererVersion;

			if (_cameraPassCtx.OutputDesc.ColorAttachments is not null && _cameraPassCtx.OutputDesc.ColorAttachments.Length != 0)
			{
				PixelFormat colorFormat = _cameraPassCtx.OutputDesc.ColorAttachments[0].Format;
				pipeline.Name = $"{resourceKey}_{colorFormat}";
			}
			else
			{
				pipeline.Name = $"{resourceKey}_DepthOnly";
			}

			_outPipeline = new(pipeline, newPipelineVersion, _vertexDataFlags, (uint)shaderSetDesc.Value.VertexLayouts.Length);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create pipeline for material '{resourceKey}' and variant '{_vertexDataFlags}'!", ex);
			_outPipeline = null!;
			return false;
		}
	}

	public override IEnumerator<ResourceHandle> GetResourceDependencies()
	{
		// Shader programs:
		if (vertexShader is not null) yield return vertexShader;
		if (geometryShader is not null) yield return geometryShader;
		if (tesselationShaderCtrl is not null && tesselationShaderEval is not null)
		{
			yield return tesselationShaderCtrl;
			yield return tesselationShaderEval;
		}
		if (pixelShader is not null) yield return pixelShader;

		// Lastly, return this material itself:
		if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
		{
			yield return handle;
		}
	}

	public static bool CreateMaterial(ResourceHandle _resourceHandle, MaterialData _data, GraphicsCore _graphicsCore, out MaterialOld? _outMaterial)
	{
		if (_graphicsCore is null || !_graphicsCore.IsInitialized)
		{
			Logger.Instance?.LogError("Cannot create material using null or uninitialized graphics core!");
			_outMaterial = null;
			return false;
		}

		Logger logger = _graphicsCore.graphicsSystem.Engine.Logger ?? Logger.Instance!;

		if (_resourceHandle is null || !_resourceHandle.IsValid)
		{
			logger.LogError("Cannot create material from null or invalid resource handle!");
			_outMaterial = null;
			return false;
		}
		// Double-check if the data actually makes any sense:
		if (_data is null || !_data.IsValid())
		{
			logger.LogError($"Material data for resource '{_resourceHandle}' is null, incomplete or invalid!");
			_outMaterial = null;
			return false;
		}

		// Assemble layout descriptions for bound resources:
		ResourceLayout? boundResourceLayout = null;
		if (_data.GetBoundResourceLayoutDesc(
			out ResourceLayoutDescription boundResourceLayoutDesc,
			out MaterialBoundResourceKeys[]? boundResourceKeys,
			out bool useExternalBoundResources))
		{
			try
			{
				boundResourceLayout = _graphicsCore.MainFactory.CreateResourceLayout(boundResourceLayoutDesc);
				boundResourceLayout.Name = $"ResLayout_Bound_{_resourceHandle.resourceKey}";
			}
			catch (Exception ex)
			{
				logger.LogException($"Failed to create resource layout for material resource '{_resourceHandle}'!", ex);
				boundResourceLayout?.Dispose();
				_outMaterial = null;
				return false;
			}
		}

		// Assemble stencil description, if required and available:
		StencilBehaviourDesc stencilDesc;
		if (_data.States.StencilFront is not null && _data.States.StencilBack is not null)
		{
			stencilDesc = new()
			{
				stencilFront = new(
					_data.States.StencilFront.Fail,
					_data.States.StencilFront.Pass,
					_data.States.StencilFront.DepthFail,
					_data.States.StencilFront.ComparisonKind),
				stencilBack = new(
					_data.States.StencilBack.Fail,
					_data.States.StencilBack.Pass,
					_data.States.StencilBack.DepthFail,
					_data.States.StencilBack.ComparisonKind),
				readMask = _data.States.StencilReadMask,
				writeMask = _data.States.StencilWriteMask,
				referenceValue = _data.States.StencilReferenceValue,
			};
		}
		else
		{
			stencilDesc = new();
		}

		// Create and initialize material instance from data.
		_outMaterial = new(_graphicsCore, _resourceHandle)
		{
			materialVersion = 1,

			vertexShader = GetResourceHandle(_data.Shaders.Vertex) ?? ResourceHandle.None,
			geometryShader = GetResourceHandle(_data.Shaders.Geometry),
			tesselationShaderCtrl = GetResourceHandle(_data.Shaders.TesselationCtrl),
			tesselationShaderEval = GetResourceHandle(_data.Shaders.TesselationEval),
			pixelShader = GetResourceHandle(_data.Shaders.Pixel) ?? ResourceHandle.None,

			depthStencilDesc = new(new()
			{
				enableDepthRead = _data.States!.EnableDepthTest,
				enableDepthWrite = _data.States.EnableDepthWrite,
				enableStencil = _data.States.EnableStencil,
				stencilBehaviour = stencilDesc,
				enableCulling = _data.States.EnableCulling,
			}, 0),

			renderModeDesc = new(new()
			{
				renderMode = _data.States.RenderMode,
				zSortingBias = _data.States.ZSortingBias,
			}, 0),

			UseExternalBoundResources = useExternalBoundResources,
			boundResourceLayout = boundResourceLayout,
			boundResourceKeys = boundResourceKeys,
			boundResourceSet = new(null!, 0),

			SimplifiedMaterialVersion = GetResourceHandle(_data.Replacements?.SimplifiedVersion),
			ShadowMapMaterialVersion = GetResourceHandle(_data.Replacements?.ShadowMap),
		};
		return true;


		// Local helper methods for getching shader handles:
		ResourceHandle? GetResourceHandle(string? _resourceKey)
		{
			return !string.IsNullOrEmpty(_resourceKey) && _resourceHandle.resourceManager.GetResource(_resourceKey, out ResourceHandle handle)
				? handle
				: null;
		}
	}

	public bool CreateMaterialData(out MaterialData _outData)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot create material data for disposed material resource!");
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
				StencilFront = new MaterialStencilBehaviourData()
				{
					Fail = dsd.stencilBehaviour.stencilFront.Fail,
					Pass = dsd.stencilBehaviour.stencilFront.Pass,
					DepthFail = dsd.stencilBehaviour.stencilFront.DepthFail,
					ComparisonKind = dsd.stencilBehaviour.stencilFront.Comparison,
				},
				StencilBack = new MaterialStencilBehaviourData()
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
			},

			Shaders = new MaterialShaderData()
			{
				IsSurfaceMaterial = true,
				Compute = string.Empty,
				Vertex = vertexShader.resourceKey,
				Geometry = geometryShader?.resourceKey ?? string.Empty,
				TesselationCtrl = tesselationShaderCtrl?.resourceKey ?? string.Empty,
				TesselationEval = tesselationShaderEval?.resourceKey ?? string.Empty,
				Pixel = pixelShader.resourceKey,
			},

			Replacements = new MaterialReplacementData()
			{
				SimplifiedVersion = SimplifiedMaterialVersion?.resourceKey ?? string.Empty,
				ShadowMap = ShadowMapMaterialVersion?.resourceKey ?? string.Empty,
			},

			Resources = [],
		};
		return _outData.IsValid();
	}

	public override string ToString()
	{
		string simplifiedVersionTxt = HasSimplifiedMaterialVersion ? $", Simplified={SimplifiedMaterialVersion!.resourceKey}" : string.Empty;
		string shadowVersionTxt = HasShadowMapMaterialVersion ? $", Shadow={ShadowMapMaterialVersion!.resourceKey}" : string.Empty;
		return $"{resourceKey}{simplifiedVersionTxt}{shadowVersionTxt}";
	}

	#endregion
}
