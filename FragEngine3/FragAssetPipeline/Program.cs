using FragAssetPipeline;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using Veldrid;

Console.WriteLine("### BEGIN ###\n");

const MeshVertexDataFlags flagsBasic = MeshVertexDataFlags.BasicSurfaceData;
const MeshVertexDataFlags flagsExt = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

ShaderDetails[] details =
[
	new("Basic_VS", "Main_Vertex", ShaderStages.Vertex, flagsExt),
	new("DefaultSurface_VS", "Main_Vertex", ShaderStages.Vertex, flagsExt),
	new("DefaultSurface_modular_PS", "Main_Pixel", ShaderStages.Fragment, flagsExt),
	new("shadows/AlphaShadow_PS", "Main_Pixel", ShaderStages.Fragment, flagsExt),
	new("shadows/DefaultShadow_PS", "Main_Pixel", ShaderStages.Fragment, flagsExt),
	new("composition/ForwardPlusLight_CompositeScene_PS", "Main_Pixel", ShaderStages.Fragment, flagsBasic),
	new("composition/ForwardPlusLight_CompositeUI_PS", "Main_Pixel", ShaderStages.Fragment, flagsBasic),
];

ShaderConfig config = new()
{
	albedoSource = ShaderAlbedoSource.SampleTexMain,
	albedoColor = RgbaFloat.White,
	useNormalMap = true,
	useParallaxMap = false,
	useParallaxMapFull = false,
	applyLighting = true,
	useAmbientLight = true,
	useLightSources = true,
	lightingModel = ShaderLightingModel.Phong,
	useShadowMaps = true,
	shadowSamplingCount = 4,
	alwaysCreateExtendedVariant = true,
};

// Export serializable shader data in FSHA-compliant format:
FshaExportOptions options = new()
{
	bundleOnlySourceIfCompilationFails = true,
	shaderStage = ShaderStages.Vertex,
	entryPointBase = "Main_Vertex",
	maxVertexVariantFlags = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData,
	compiledDataTypeFlags = CompiledShaderDataType.ALL,
	supportedFeatures = config,
};

foreach (var detail in details)
{
	options.shaderStage = detail.stage;
	options.entryPointBase = detail.entryPointNameBase;
	options.maxVertexVariantFlags = detail.maxVertexFlags;
	options.entryPoints = null;

	ShaderProcess.CompileShaderToFSHA(detail.fileName, options);
}

Console.WriteLine("\n#### END ####");
