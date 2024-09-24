using FragEngine3.Graphics.Lighting;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

/// <summary>
/// Description structure for standard shader configurations. This type contains
/// a comprehensive list of feature flags and paramaters that will be set at
/// compile-time, or using the shader pre-processor. Most of these flags may be
/// set using #define macros.<para/>
/// This config type may however also be used as a simple description of a shader's
/// features that are compliant with the engine's standard shader system.
/// </summary>
public struct ShaderConfig
{
	#region Fields

	// Albedo:
	public ShaderAlbedoSource albedoSource;			// Which value to initialize albedo/main color from. If source is texture, "TexMain" will be added.
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
	public ShaderLightingModel lightingModel;		// For lighting, which lighting model to use for light sources.
	public bool useShadowMaps;                          // For lighting, whether to use shadow maps to mask out light coming from light sources.
	public uint shadowSamplingCount;					// For shadow maps, how many depth samples are averaged to calculate depth per pixel.
	public uint indirectLightResolution;				// For indirect lighting, how many samples per side of a grid to use for approximating nearby indirect light scattering.

	// Variants:
	public bool alwaysCreateExtendedVariant;			// Whether to always also create an '_Ext' variant, even if no feature requires the additional geometry data.
	public bool alwaysCreateBlendShapeVariant;          // Whether to always also create '_Blend' variants, even if no feature requires the additional blending data. Unnecessary for pixel shaders.
	public bool alwaysCreateAnimatedVariant;            // Whether to always also create '_Anim' variants, even if no feature requires the additional bone animation data. Unnecessary for pixel shaders.

	#endregion
	#region Properties

	public static ShaderConfig ConfigWhiteLit => new()
	{
		albedoSource = ShaderAlbedoSource.Color,
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
		lightingModel = ShaderLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 1,
		indirectLightResolution = 0u,
	};

	public static ShaderConfig ConfigMainLit => new()
	{
		albedoSource = ShaderAlbedoSource.SampleTexMain,
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
		lightingModel = ShaderLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 1,
		indirectLightResolution = 0u,
	};

	public static ShaderConfig ConfigMainNormalsLit => new()
	{
		albedoSource = ShaderAlbedoSource.SampleTexMain,
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
		lightingModel = ShaderLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 1,
		indirectLightResolution = 0u,
	};

	#endregion
	#region Methods

	/// <summary>
	/// Simplify configuration by propagating feature flags down the dependency hierarchy.
	/// This will disable and unset all flags and values that were already disabled by
	/// parent features.<para/>
	/// Example: If light sources are disabled, shadow maps won't work either and can be
	/// disabled as well.
	/// </summary>
	public void PropagateFlagStatesToHierarchy()
	{
		// Albedo:
		if (albedoSource != ShaderAlbedoSource.SampleTexMain)
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
			lightingModel = ShaderLightingModel.Phong;
			indirectLightResolution = 0u;
		}
	}

	/// <summary>
	/// Create a ShaderGen config that has the highest feature set of two given configurations.
	/// </summary>
	/// <param name="_a">The first config.</param>
	/// <param name="_b">The second config.</param>
	/// <param name="_propagateAfterMax">Whether to call '<see cref="PropagateFlagStatesToHierarchy"/>'
	/// on the result of the max operation.</param>
	/// <returns>A new standard shader configuration with the highest feature set combination.</returns>
	public static ShaderConfig Max(ShaderConfig _a, ShaderConfig _b, bool _propagateAfterMax = true)
	{
		bool useParallaxMap = _a.useParallaxMap || _b.useParallaxMap;
		bool applyLighting = _a.applyLighting || _b.applyLighting;
		bool useLightSources = applyLighting && (_a.useLightSources || _b.useLightSources);

		ShaderConfig max = new()
		{
			// Albedo:
			albedoSource = (ShaderAlbedoSource)Math.Max((int)_a.albedoSource, (int)_b.albedoSource),
			samplerTexMain = !string.IsNullOrEmpty(_a.samplerTexMain)
				? _a.samplerTexMain
				: _a.samplerTexMain,

			// Normals:
			useNormalMap = _a.useNormalMap || _b.useNormalMap,
			useParallaxMap = useParallaxMap,
			useParallaxMapFull = useParallaxMap && (_a.useParallaxMapFull || _b.useParallaxMapFull),
			samplerTexNormal = !string.IsNullOrEmpty(_a.samplerTexNormal)
				? _a.samplerTexNormal
				: _a.samplerTexNormal,

			// Lighting:
			applyLighting = applyLighting,
			useAmbientLight = applyLighting && (_a.useAmbientLight || _b.useAmbientLight),
			useLightMaps = applyLighting && (_a.useLightMaps || _b.useLightMaps),
			useLightSources = useLightSources,
			lightingModel = (ShaderLightingModel)Math.Max((int)_a.lightingModel, (int)_b.lightingModel),
			useShadowMaps = useLightSources && (_a.useShadowMaps || _b.useShadowMaps),
			shadowSamplingCount = useLightSources ? Math.Max(_a.shadowSamplingCount, _b.shadowSamplingCount) : 0,
			indirectLightResolution = useLightSources ? Math.Max(_a.indirectLightResolution, _b.indirectLightResolution) : 0,

			// Variants:
			alwaysCreateExtendedVariant = _a.alwaysCreateExtendedVariant || _b.alwaysCreateExtendedVariant,
			alwaysCreateBlendShapeVariant = _a.alwaysCreateBlendShapeVariant || _b.alwaysCreateBlendShapeVariant,
			alwaysCreateAnimatedVariant = _a.alwaysCreateAnimatedVariant || _b.alwaysCreateAnimatedVariant,
		};

		if (_propagateAfterMax)
		{
			max.PropagateFlagStatesToHierarchy();
		}
		return max;
	}

	/// <summary>
	/// Update variants creation flags to match a given set of vertex data flags.
	/// </summary>
	/// <param name="_vertexDataFlags">Flags of the vertex data that may be used by the
	/// shader's variants.</param>
	public void SetVariantFlagsFromMeshVertexData(MeshVertexDataFlags _vertexDataFlags)
	{
		alwaysCreateExtendedVariant = _vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
		alwaysCreateBlendShapeVariant = _vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
		alwaysCreateAnimatedVariant = _vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations);
	}

	/// <summary>
	/// Get vertex data flags corresponding to all variant creation flags.
	/// </summary>
	/// <returns>A set of vertex data flags.</returns>
	public readonly MeshVertexDataFlags GetVertexDataForVariantFlags()
	{
		MeshVertexDataFlags flags = MeshVertexDataFlags.BasicSurfaceData;

		if (alwaysCreateExtendedVariant) flags |= MeshVertexDataFlags.ExtendedSurfaceData;
		if (alwaysCreateBlendShapeVariant) flags |= MeshVertexDataFlags.BlendShapes;
		if (alwaysCreateAnimatedVariant) flags |= MeshVertexDataFlags.Animations;

		return flags;
	}

	/// <summary>
	/// Creates a descriptive text that encodes all feature flags into a compact serializable string format.
	/// </summary>
	/// <returns>The descriptive string.</returns>
	public readonly string CreateDescriptionTxt()
	{
		// Albedo:
		// Format: "Ac"
		char albedoSrc = BoolToCustom(albedoSource == ShaderAlbedoSource.SampleTexMain, 't', 'c');

		// Normals:
		// Format: "Nyn"
		char normalsYN = BoolToYN(useNormalMap);
		char parallaxYN = BoolToYN(useParallaxMap);
		char parallaxFull = BoolTo01(useParallaxMap && useParallaxMapFull);

		// Lighting:
		// Format: "Ly101p145"
		char lightingYN = BoolToYN(applyLighting);
		char useAmbient = BoolTo01(applyLighting && useAmbientLight);
		char useLgtMaps = BoolTo01(applyLighting && useLightMaps);
		char useLgtSrcs = BoolTo01(applyLighting && useLightSources);
		char lightModel = BoolToCustom(applyLighting && useLightSources && lightingModel == ShaderLightingModel.Phong, 'p', '?');
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
		if (albedoSource == ShaderAlbedoSource.SampleTexMain && !string.IsNullOrEmpty(samplerTexMain))
		{
			// Format: "As_SamplerMain"
			txt += $"_As={samplerTexMain}";
		}
		else if (albedoSource == ShaderAlbedoSource.Color)
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

	/// <summary>
	/// Try to parse a descriptive text into a standard shader configuration.
	/// </summary>
	/// <param name="_txt">The descriptive text, may not be null or blank.</param>
	/// <param name="_outConfig">Outputs the parsed shader configuration.</param>
	/// <returns>True if the string could be parsed successfully, false if parsing failed.</returns>
	public static bool TryParseDescriptionTxt(string _txt, out ShaderConfig _outConfig)
	{
		if (string.IsNullOrEmpty(_txt))
		{
			_outConfig = default;
			return false;
		}

		bool success = true;
		ShaderConfig config = new();

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
					config.albedoSource = ShaderAlbedoSource.Color;
					return true;
				case 'l':
					string colorLiteral = _part.Substring(3);
					config.albedoColor = Color32.ParseHexString(colorLiteral).ToRgbaFloat();
					return true;
				case 's':
					config.samplerTexMain = _part.Substring(3);
					return true;
				case 't':
					config.albedoSource = ShaderAlbedoSource.SampleTexMain;
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
						config.lightingModel = ShaderLightingModel.Phong;
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

	/// <summary>
	/// Creates a list of pre-processor '#define' strings that can be prepended to a shader's source code to enable
	/// or control certain features.
	/// </summary>
	/// <param name="_includeVariantDefines">Whether to also include defines for vertex data variant flags.</param>
	/// <returns>A list of code lines with '#define' statements that can be added to a shader's source code.</returns>
	public List<string> GetFeatureDefineStrings(bool _includeVariantDefines)
	{
		const string featureDefinePrefix = "#define FEATURE_";
		const string variantDefinePrefix = "#define VARIANT_";

		List<string> defineStrings = [];

		// Albedo:
		if (albedoSource == ShaderAlbedoSource.Color)
		{
			string colorValueTxt = $"half4({albedoColor.R:0.###}, {albedoColor.G:0.###}, {albedoColor.B:0.###}, {albedoColor.A:0.###})";
			defineStrings.Add(featureDefinePrefix + "ALBEDO_COLOR " + colorValueTxt);
		}
		else if (albedoSource == ShaderAlbedoSource.SampleTexMain)
		{
			defineStrings.Add(featureDefinePrefix + "ALBEDO_TEXTURE 1");
		}

		// Normals & Parallax:
		if (useNormalMap)
		{
			defineStrings.Add(featureDefinePrefix + "NORMALS");
		}
		if (useParallaxMap)
		{
			defineStrings.Add(featureDefinePrefix + "PARALLAX");
			if (useParallaxMapFull)
			{
				defineStrings.Add(featureDefinePrefix + "PARALLAX_FULL");
			}
		}

		// Lighting:
		if (applyLighting)
		{
			defineStrings.Add(featureDefinePrefix + "LIGHT");
			if (useAmbientLight)
			{
				defineStrings.Add(featureDefinePrefix + "LIGHT_AMBIENT");
			}
			if (useLightMaps)
			{
				defineStrings.Add(featureDefinePrefix + "LIGHT_LIGHTMAPS");
			}
			// Light sources:
			if (useLightSources)
			{
				string modelShort = lightingModel switch
				{
					ShaderLightingModel.Phong => "P",
					ShaderLightingModel.BlinnPhong => "BP",
					ShaderLightingModel.Beckmann => "B",
					_ => string.Empty,
				};
				defineStrings.Add(featureDefinePrefix + "LIGHT_SOURCES");
				defineStrings.Add(featureDefinePrefix + "LIGHT_MODEL " + modelShort);

				// Shadow maps:
				if (useShadowMaps)
				{
					defineStrings.Add(featureDefinePrefix + "LIGHT_SHADOWMAPS");
					defineStrings.Add(featureDefinePrefix + $"LIGHT_SHADOWMAPS_RES {LightConstants.SHADOW_RESOLUTION}");
					defineStrings.Add(featureDefinePrefix + "LIGHT_SHADOWMAPS_AA " + Math.Max(shadowSamplingCount, 1).ToString());
					if (indirectLightResolution != 0)
					{
						defineStrings.Add(featureDefinePrefix + "LIGHT_INDIRECT " + indirectLightResolution.ToString());
					}
				}
			}
		}

		// Vertex variant flags:
		if (_includeVariantDefines)
		{
			if (alwaysCreateExtendedVariant)
			{
				defineStrings.Add(variantDefinePrefix + "EXTENDED");
			}
			if (alwaysCreateBlendShapeVariant)
			{
				defineStrings.Add(variantDefinePrefix + "BLENDSHAPES");
			}
			if (alwaysCreateAnimatedVariant)
			{
				defineStrings.Add(variantDefinePrefix + "ANIMATED");
			}
		}

		return defineStrings;
	}

	#endregion
}
