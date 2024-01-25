using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public struct DefaultShaderConfig
{
	#region Fields

	// Albedo:
	public DefaultShaderAlbedoSource albedoSource;		// Which value to initialize albedo/main color from. If source is texture, "TexMain" will be added.
	public RgbaFloat albedoColor;						// If albedo source is color, initialize with this value. Implemented as inlined literal.
	public string? samplerTexMain;						// If albedo source is TexMain, use a sampler with this name. "SamplerMain" is used if null or empty.

	// Normals:
	public bool useNormalMap;							// Whether to use normal maps, which will be used for all further shading. "VertexOutput_Extended" and "TexNormal" are added.
	public string? samplerTexNormal;					// For normal maps, use a sampler with this name. Main texture sampler (or "SamplerMain") is used if null or empty.

	// Lighting:
	public bool applyLighting;							// Whether to apply any lighting at all. Lighting calculations are added after albedo and normals.
	public bool useAmbientLight;                        // For lighting, whether to use ambient lighting from scene constant buffer as basic unlit lighting value. "CBScene" is added.
	public bool useLightMaps;                           // For lighting, whether to use light maps to add additional static precalculated lighting.
	public bool useLightSources;						// For lighting, whether to light source in the scene to light up surfaces. "BufLights" buffer and "Light" struct are added.
	public DefaultShaderLightingModel lightingModel;    // For lighting, which lighting model to use for light sources.
	public bool useShadowMaps;                          // For lighting, whether to use shadow maps to mask out light coming from light sources.

	#endregion
	#region Properties

	public static DefaultShaderConfig ConfigWhiteLit => new()
	{
		albedoSource = DefaultShaderAlbedoSource.Color,
		albedoColor = RgbaFloat.White,
		samplerTexMain = null,

		useNormalMap = false,
		samplerTexNormal = null,

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = DefaultShaderLightingModel.Phong,
		useShadowMaps = true,
	};

	public static DefaultShaderConfig ConfigMainLit => new()
	{
		albedoSource = DefaultShaderAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		samplerTexMain = "SamplerMain",

		useNormalMap = false,
		samplerTexNormal = null,

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = DefaultShaderLightingModel.Phong,
		useShadowMaps = true,
	};

	public static DefaultShaderConfig ConfigMainNormalsLit => new()
	{
		albedoSource = DefaultShaderAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		samplerTexMain = "SamplerMain",

		useNormalMap = true,
		samplerTexNormal = "SamplerMain",

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = DefaultShaderLightingModel.Phong,
		useShadowMaps = true,
	};

	#endregion
}
