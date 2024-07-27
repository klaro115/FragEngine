using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public struct ShaderGenConfig
{
	#region Fields

	// Albedo:
	public ShaderGenAlbedoSource albedoSource;			// Which value to initialize albedo/main color from. If source is texture, "TexMain" will be added.
	public RgbaFloat albedoColor;						// If albedo source is color, initialize with this value. Implemented as inlined literal.
	public string? samplerTexMain;						// If albedo source is TexMain, use a sampler with this name. "SamplerMain" is used if null or empty.

	// Normals:
	public bool useNormalMap;                           // Whether to use normal maps, which will be used for all further shading. "TexNormal" is added.
	public bool useParallaxMap;							// Whether to use height maps to offset UVs for additional simulated depth. "TexParallax" is added.
	public bool useParallaxMapFull;                     // Whether to use full iteratively traced parallax with occlusion, instead of just simple UV offsetting.
	public string? samplerTexNormal;					// For normal maps, use a sampler with this name. Main texture sampler (or "SamplerMain") is used if null or empty.

	// Lighting:
	public bool applyLighting;							// Whether to apply any lighting at all. Lighting calculations are added after albedo and normals.
	public bool useAmbientLight;                        // For lighting, whether to use ambient lighting from scene constant buffer as basic unlit lighting value. "CBScene" is added.
	public bool useLightMaps;                           // For lighting, whether to use light maps to add additional static precalculated lighting.
	public bool useLightSources;						// For lighting, whether to use light sources in the scene to light up surfaces. "BufLights" buffer and "Light" struct are added.
	public ShaderGenLightingModel lightingModel;		// For lighting, which lighting model to use for light sources.
	public bool useShadowMaps;                          // For lighting, whether to use shadow maps to mask out light coming from light sources.
	public uint shadowSamplingCount;					// For shadow maps, how many depth samples are averaged to calculate depth per pixel.
	public uint indirectLightResolution;				// For indirect lighting, how many samples per side of a grid to use for approximating nearby indirect light scattering.

	// Variants:
	public bool alwaysCreateExtendedVariant;			// Whether to always also create an '_Ext' variant, even if no feature requires the additional geometry data.
	public bool alwaysCreateBlendShapeVariant;          // Whether to always also create '_Blend' variants, even if no feature requires the additional blending data. Unnecessary for pixel shaders.
	public bool alwaysCreateAnimatedVariant;            // Whether to always also create '_Anim' variants, even if no feature requires the additional bone animation data. Unnecessary for pixel shaders.

	#endregion
	#region Properties

	public static ShaderGenConfig ConfigWhiteLit => new()
	{
		albedoSource = ShaderGenAlbedoSource.Color,
		albedoColor = RgbaFloat.White,
		samplerTexMain = null,

		useNormalMap = false,
		useParallaxMap = false,
		useParallaxMapFull = false,
		samplerTexNormal = null,

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = ShaderGenLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 1,
		indirectLightResolution = 0u,
	};

	public static ShaderGenConfig ConfigMainLit => new()
	{
		albedoSource = ShaderGenAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		samplerTexMain = null,

		useNormalMap = false,
		useParallaxMap = false,
		useParallaxMapFull = false,
		samplerTexNormal = null,

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = ShaderGenLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 1,
		indirectLightResolution = 0u,
	};

	public static ShaderGenConfig ConfigMainNormalsLit => new()
	{
		albedoSource = ShaderGenAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		samplerTexMain = null,

		useNormalMap = true,
		useParallaxMap = false,
		useParallaxMapFull = false,
		samplerTexNormal = null,

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = ShaderGenLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 1,
		indirectLightResolution = 0u,
	};

	#endregion
	#region Methods

	public void PropagateFlagStatesToHierarchy()
	{
		// Albedo:
		if (albedoSource != ShaderGenAlbedoSource.SampleTexMain)
		{
			samplerTexMain = null;
		}

		// Normals:
		if (!useNormalMap)
		{
			samplerTexNormal = null;
		}
		useParallaxMapFull &= useParallaxMap;

		// Lighting:
		useAmbientLight &= applyLighting;
		useLightMaps &= applyLighting;
		useLightSources &= applyLighting;
		useShadowMaps &= useLightSources;
		if (!useShadowMaps)
		{
			shadowSamplingCount = 0;
		}
		if (!useLightSources)
		{
			lightingModel = ShaderGenLightingModel.Phong;
			indirectLightResolution = 0u;
		}
	}

	public readonly string CreateDescriptionTxt()
	{
		// Albedo:
		// Format: "Ac"
		char albedoSrc = BoolToCustom(albedoSource == ShaderGenAlbedoSource.SampleTexMain, 't', 'c');

		// Normals:
		// Format: "Nyn"
		char normalsYN = BoolToYN(useNormalMap);
		char parallaxYN = BoolToYN(useParallaxMap);
		char parallaxFull = BoolTo01(useParallaxMap && useParallaxMapFull);

		// Lighting:
		// Format: "Ly101p1"
		char lightingYN = BoolToYN(applyLighting);
		char useAmbient = BoolTo01(applyLighting && useAmbientLight);
		char useLgtMaps = BoolTo01(applyLighting && useLightMaps);
		char useLgtSrcs = BoolTo01(applyLighting && useLightSources);
		char lightModel = BoolToCustom(applyLighting && useLightSources && lightingModel == ShaderGenLightingModel.Phong, 'p', '?');
		char useShaMaps = BoolTo01(applyLighting && useLightSources && useShadowMaps);
		char shaSampNum = UintToHex(Math.Min(shadowSamplingCount, 8));
		char indirectRes = UintToHex(Math.Min(indirectLightResolution, 15));

		// Variants:
		// Format: "V100"
		char variantExt = BoolTo01(alwaysCreateExtendedVariant);
		char variantBle = BoolTo01(alwaysCreateBlendShapeVariant);
		char variantAni = BoolTo01(alwaysCreateAnimatedVariant);

		// Assemble base text containing only flags:
		// Format: "Ac_Nyn_Ly101p1_V100"
		string txt = $"A{albedoSrc}_N{normalsYN}{parallaxYN}{parallaxFull}_L{lightingYN}{useAmbient}{useLgtMaps}{useLgtSrcs}{lightModel}{useShaMaps}{shaSampNum}{indirectRes}_V{variantExt}{variantBle}{variantAni}";

		// Albedo start color literal or sampler name:
		if (albedoSource == ShaderGenAlbedoSource.SampleTexMain && !string.IsNullOrEmpty(samplerTexMain))
		{
			// Format: "As_SamplerMain"
			txt += $"_As={samplerTexMain}";
		}
		else if (albedoSource == ShaderGenAlbedoSource.Color)
		{
			// Format: "Al_0xFF0000FF"
			txt += $"_Al={new Color32(albedoColor).ToHexStringLower()}";
		}

		// Normal map sampler name:
		if (useNormalMap && !string.IsNullOrEmpty(samplerTexNormal) && string.CompareOrdinal(samplerTexMain, samplerTexNormal) != 0)
		{
			// Format: "Ns_SamplerNormals"
			txt += $"_Ns={samplerTexNormal}";
		}

		return txt;


		// Local helper methods for converting values to parsable characters:
		static char BoolToYN(bool _value) => _value ? 'y' : 'n';
		static char BoolTo01(bool _value) => _value ? '1' : '0';
		static char BoolToCustom(bool _value, char _y, char _n) => _value ? _y : _n;
		static char UintToHex(uint _value) => _value < 10
			? (char)('0' + _value)
			: (char)('A' + _value);
	}

	public static bool TryParseDescriptionTxt(string _txt, out ShaderGenConfig _outConfig)
	{
		if (string.IsNullOrEmpty(_txt))
		{
			_outConfig = default;
			return false;
		}

		bool success = true;
		ShaderGenConfig config = new();

		string[] parts = _txt.Split('_', StringSplitOptions.RemoveEmptyEntries);
		foreach (string part in parts)
		{
			char cLead = part[0];
			success &= cLead switch
			{
				'A' => TryParseAlbedo(part),
				'N' => TryParseNormals(part),
				'L' => TryParseLighting(part),
				'V' => TryParseVariants(part),
				_ => false,
			};
		}

		_outConfig = config;
		return true;


		bool TryParseAlbedo(string _part)
		{
			if (_part.Length < 2) return false;
			char cType = _part[1];
			switch (cType)
			{
				case 'c':
					config.albedoSource = ShaderGenAlbedoSource.Color;
					return true;
				case 'l':
					string colorLiteral = _part.Substring(3);
					config.albedoColor = Color32.ParseHexString(colorLiteral).ToRgbaFloat();
					return true;
				case 's':
					config.samplerTexMain = _part.Substring(3);
					return true;
				case 't':
					config.albedoSource = ShaderGenAlbedoSource.SampleTexMain;
					return true;
				default:
					return false;
			}
		}
		bool TryParseNormals(string _part)
		{
			if (_part.Length < 2) return false;
			char cType = _part[1];
			switch (cType)
			{
				case 'n':
					config.useNormalMap = false;
					break;
				case 's':
					config.samplerTexNormal = _part.Substring(3);
					return true;
				case 'y':
					config.useNormalMap = true;
					break;
				default:
					return false;
			}
			if (cType != 's')
			{
				config.useParallaxMap = _part.Length >= 3 && _part[2] == 'y';
				config.useParallaxMapFull = _part.Length >= 4 && _part[3] == '1';
			}
			return true;
		}
		bool TryParseLighting(string _part)
		{
			if (_part.Length < 2) return false;
			config.applyLighting = _part[1] == 'y';
			if (config.applyLighting && _part.Length >= 7)
			{
				config.useAmbientLight = _part[2] == '1';
				config.useLightMaps = _part[3] == '1';
				config.useLightSources = _part[4] == '1';
				switch (_part[5])
				{
					case 'p':
						config.lightingModel = ShaderGenLightingModel.Phong;
						break;
					default:
						return false;
				}
				config.useShadowMaps = _part[6] == '1';
				config.shadowSamplingCount = _part.Length > 7 ? HexToUint(_part[7]) : 0;
				config.indirectLightResolution = _part.Length > 8 ? HexToUint(_part[8]) : 0;
			}
			return true;

			static uint HexToUint(char _hex) => _hex >= 'A' && _hex <= 'F'
				? (uint)(10 + _hex - 'A')
				: (uint)(_hex - '0');
		}
		bool TryParseVariants(string _part)
		{
			int len = _part.Length;
			config.alwaysCreateExtendedVariant = len >= 2 && _part[1] == '1';
			config.alwaysCreateBlendShapeVariant = len >= 3 && _part[2] == '1';
			config.alwaysCreateAnimatedVariant = len >= 4 && _part[3] == '1';
			return true;
		}
	}

	#endregion
}
