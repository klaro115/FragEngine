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

	// Variants:
	public bool alwaysCreateExtendedVariant;			// Whether to always also create an '_Ext' variant, even if no feature requires the additional geometry data.
	public bool alwaysCreateBlendShapeVariant;          // Whether to always also create '_Blend' variants, even if no feature requires the additional blending data. Unnecessary for pixel shaders.
	public bool alwaysCreateAnimatedVariant;            // Whether to always also create '_Anim' variants, even if no feature requires the additional bone animation data. Unnecessary for pixel shaders.

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

	public static DefaultShaderConfig ConfigMainNormalsLit => new()
	{
		albedoSource = DefaultShaderAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		samplerTexMain = null,

		useNormalMap = true,
		samplerTexNormal = null,

		applyLighting = true,
		useAmbientLight = true,
		useLightMaps = false,
		useLightSources = true,
		lightingModel = DefaultShaderLightingModel.Phong,
		useShadowMaps = true,
	};

	#endregion
	#region Methods

	public readonly string CreateDescriptionTxt()
	{
		// Albedo:
		char albedoSrc = BoolToCustom(albedoSource == DefaultShaderAlbedoSource.SampleTexMain, 't', 'c');

		// Normals:
		char normalsYN = BoolToYN(useNormalMap);

		// Lighting:
		char lightingYN = BoolToYN(applyLighting);
		char useAmbient = BoolTo01(applyLighting && useAmbientLight);
		char useLgtMaps = BoolTo01(applyLighting && useLightMaps);
		char useLgtSrcs = BoolTo01(applyLighting && useLightSources);
		char lightModel = BoolToCustom(applyLighting && useLightSources && lightingModel == DefaultShaderLightingModel.Phong, 'p', '?');
		char useShaMaps = BoolTo01(applyLighting && useLightSources && useShadowMaps);

		// Variants:
		char variantExt = BoolTo01(alwaysCreateExtendedVariant);
		char variantBle = BoolTo01(alwaysCreateBlendShapeVariant);
		char variantAni = BoolTo01(alwaysCreateAnimatedVariant);

		// Assemble base text containing only flags:
		string txt = $"A{albedoSrc}_N{normalsYN}_L{lightingYN}{useAmbient}{useLgtMaps}{useLgtSrcs}{lightModel}{useShaMaps}_V{variantExt}{variantBle}{variantAni}";

		// Albedo start color literal or sampler name:
		if (albedoSource == DefaultShaderAlbedoSource.SampleTexMain && !string.IsNullOrEmpty(samplerTexMain))
		{
			txt += $"_As={samplerTexMain}";
		}
		else if (albedoSource == DefaultShaderAlbedoSource.Color)
		{
			txt += $"_Al={new Color32(albedoColor).ToHexStringLower()}";
		}

		// Normal map sampler name:
		if (useNormalMap && !string.IsNullOrEmpty(samplerTexNormal) && string.CompareOrdinal(samplerTexMain, samplerTexNormal) != 0)
		{
			txt += $"_Ns={samplerTexNormal}";
		}

		return txt;

		static char BoolToYN(bool _value) => _value ? 'y' : 'n';
		static char BoolTo01(bool _value) => _value ? '1' : '0';
		static char BoolToCustom(bool _value, char _y, char _n) => _value ? _y : _n;
	}

	public static bool TryParseDescriptionTxt(string _txt, out DefaultShaderConfig _outConfig)
	{
		if (string.IsNullOrEmpty(_txt))
		{
			_outConfig = default;
			return false;
		}

		bool success = true;
		DefaultShaderConfig config = new DefaultShaderConfig();

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
					config.albedoSource = DefaultShaderAlbedoSource.Color;
					return true;
				case 'l':
					string colorLiteral = _part.Substring(3);
					config.albedoColor = Color32.ParseHexString(colorLiteral).ToRgbaFloat();
					return true;
				case 's':
					config.samplerTexMain = _part.Substring(3);
					return true;
				case 't':
					config.albedoSource = DefaultShaderAlbedoSource.SampleTexMain;
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
					return true;
				case 's':
					config.samplerTexNormal = _part.Substring(3);
					return true;
				case 'y':
					config.useNormalMap = true;
					return true;
				default:
					return false;
			}
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
						config.lightingModel = DefaultShaderLightingModel.Phong;
						break;
					default:
						return false;
				}
				config.useShadowMaps = _part[6] == '1';
			}
			return true;
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
