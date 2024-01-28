using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.ShaderGen.Features;

public static class ShaderGenLighting
{
	#region Methods

	private static bool WriteType_Lighting_Light(in ShaderGenContext _ctx)
	{
		const string nameType = "Light";
		if (_ctx.globalDeclarations.Contains(nameType)) return true;

		string typeNameVec = _ctx.language == ShaderGenLanguage.GLSL
			? "vec3"
			: "float3";
		string typeNameMtx = _ctx.language == ShaderGenLanguage.GLSL
			? "mtx4"
			: "float4x4";

		_ctx.resources
			.AppendLine("struct Light")
			.AppendLine("{")
			.AppendLine($"    {typeNameVec} lightColor;")
			.AppendLine("    float lightIntensity;")
			.AppendLine($"    {typeNameVec} lightPosition;")
			.AppendLine("    uint lightType;")
			.AppendLine($"    {typeNameVec} lightDirection;")
			.AppendLine("    float lightSpotMinDot;")
			.AppendLine($"    {typeNameMtx} mtxShadowWorld2Clip;")
			.AppendLine("    uint shadowMapIdx;")
			.AppendLine("    float shadowBias;")
			.AppendLine("};");

		_ctx.globalDeclarations.Add(nameType);
		return true;
	}

	private static bool WriteResource_Lighting_BufLights(in ShaderGenContext _ctx)
	{
		const string nameRes = "BufLights";
		if (_ctx.globalDeclarations.Contains(nameRes)) return true;

		bool success = true;

		// Ensure the light type is also defined:
		success &= WriteType_Lighting_Light(in _ctx);

		// Metal:
		if (_ctx.language == ShaderGenLanguage.Metal)
		{
			foreach (ShaderGenVariant variant in _ctx.variants)
			{
				if (variant.arguments.Length != 0)
				{
					variant.arguments.Append(", ");
				}
				variant.arguments.Append("device const Light* BufLights [[ buffer( 3 ) ]]");
			}
		}
		// HLSL & GLSL:
		else
		{
			_ctx.resources.AppendLine("StructuredBuffer<Light> BufLights : register(ps, t0);");
		}

		_ctx.globalDeclarations.Add(nameRes);
		return success;
	}

	private static bool WriteResource_Lighting_TexShadowMaps(in ShaderGenContext _ctx)
	{
		// TexShadowMaps:
		const string nameTex = "TexShadowMaps";
		if (!_ctx.globalDeclarations.Contains(nameTex))
		{
			// Metal:
			if (_ctx.language == ShaderGenLanguage.Metal)
			{
				foreach (ShaderGenVariant variant in _ctx.variants)
				{
					if (variant.arguments.Length != 0)
					{
						variant.arguments.Append(", ");
					}
					variant.arguments.Append("texture2d<half, access::read> TexOpaqueColor [[ texture( 0 ) ]]");
				}
			}
			// HLSL & GLSL:
			else
			{
				_ctx.resources.AppendLine("Texture2DArray<half> TexShadowMaps : register(ps, t1);");
			}
			_ctx.globalDeclarations.Add(nameTex);
		}

		// SamplerShadowMaps:
		const string nameSampler = "SamplerShadowMaps";
		if (!_ctx.globalDeclarations.Contains(nameSampler))
		{
			// Metal:
			if (_ctx.language == ShaderGenLanguage.Metal)
			{
				foreach (ShaderGenVariant variant in _ctx.variants)
				{
					if (variant.arguments.Length != 0)
					{
						variant.arguments.Append(", ");
					}
					//TODO: no idea how this is done. This language is a convoluted mess.
				}
			}
			// HLSL & GLSL:
			else
			{
				_ctx.resources.AppendLine("SamplerState SamplerShadowMaps : register(ps, s0);");
			}
			_ctx.globalDeclarations.Add(nameTex);
			_ctx.globalDeclarations.Add(nameSampler);
		}

		return true;
	}

	private static bool WriteFunction_Lighting_CalculateAmbientLight(in ShaderGenContext _ctx)
	{
		const string nameFunc = "CalculateAmbientLight";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		bool success = true;

		success &= ShaderGenUniforms.WriteConstantBuffer_CBScene(in _ctx);

		// Write function header:
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			["half3 CalculateAmbientLight(in float3 _worldNormal)"],
			["half3 CalculateAmbientLight(const float3& _worldNormal)"],
			null);

		// Write function body:
		_ctx.functions
			.AppendLine("{")
			.AppendLine("    half dotY = (half)dot(_worldNormal, float3(0, 1, 0));")
			.AppendLine("    half wLow = max(-dotY, 0);")
			.AppendLine("    half wHigh = max(dotY, 0);")
			.AppendLine("    half wMid = 1.0 - wHigh - wLow;")
			.AppendLine("    return (wLow * (half4)ambientLightLow + wHigh * (half4)ambientLightHigh + wMid * (half4)ambientLightMid).xyz;")
			.AppendLine("}")
			.AppendLine();

		_ctx.globalDeclarations.Add(nameFunc);
		return success;
	}

	private static bool WriteFunction_Lighting_CalculateLightMaps(in ShaderGenContext _ctx)
	{
		//TODO
		return true;
	}

	private static bool WriteFunction_Lighting_CalculatePhongLighting(in ShaderGenContext _ctx)
	{
		const string nameFunc = "CalculatePhongLighting";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		bool success = true;

		// Write function header:
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			["half3 CalculatePhongLighting(in Light _light, in float3 _worldPosition, in float3 _worldNormal)"],
			["half3 CalculatePhongLighting(device const Light& _light, const float3& _worldPosition, const float3& _worldNormal)"],
			null);

		_ctx.functions
			.AppendLine("{")
			.AppendLine("    half3 lightIntens = (half3)(_light.lightColor * _light.lightIntensity);")
			.AppendLine("    float3 lightRayDir;")
			.AppendLine()
			.AppendLine("    // Directional light:")
			.AppendLine("    if (_light.lightType == 2)")
			.AppendLine("    {")
			.AppendLine("        lightRayDir = _light.lightDirection;")
			.AppendLine("    }")
			.AppendLine("    // Point or Spot light:")
			.AppendLine("    else")
			.AppendLine("    {")
			.AppendLine("        float3 lightOffset = _worldPosition - _light.lightPosition;")
			.AppendLine("        lightIntens /= (half)dot(lightOffset, lightOffset);")
			.AppendLine("        lightRayDir = normalize(lightOffset);")
			.AppendLine()
			.AppendLine("        // Spot light angle:")
			.AppendLine("        if (_light.lightType == 1 && dot(_light.lightDirection, lightRayDir) < _light.lightSpotMinDot)")
			.AppendLine("        {")
			.AppendLine("            lightIntens = half3(0, 0, 0);")
			.AppendLine("        }")
			.AppendLine("    }")
			.AppendLine()
			.AppendLine("    half lightDot = max(-(half)dot(lightRayDir, _worldNormal), 0.0);")
			.AppendLine("    return lightIntens.xyz * lightDot;")
			.AppendLine("}")
			.AppendLine();

		_ctx.globalDeclarations.Add(nameFunc);
		return success;
	}

	private static bool WriteFunction_Lighting_CalculateShadowMapLightWeight(in ShaderGenContext _ctx)
	{
		const string nameFunc = "CalculateShadowMapLightWeight";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		bool success = true;

		success &= WriteResource_Lighting_TexShadowMaps(in _ctx);

		// Write define for "SHADOW_EDGE_FACE_SCALE":
		_ctx.functions
			.AppendLine("#define SHADOW_EDGE_FACE_SCALE 10.0")
			.AppendLine();

		// Write function header:
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			["half CalculateShadowMapLightWeight(in Light _light, in float3 _worldPosition, in float3 _worldNormal)"],
			["half CalculateShadowMapLightWeight(device const Light& _light, const float3& _worldPosition, const float3& _worldNormal)"],
			null);

		// Write function body:
		string funcNameLerp = _ctx.language == ShaderGenLanguage.HLSL
			? "lerp"
			: "mix";

		_ctx.functions
			.AppendLine("{")
			.AppendLine("    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:")
			.AppendLine("    float4 worldPosBiased = float4(_worldPosition + _worldNormal * _light.shadowBias, 1);")
			.AppendLine()
			.AppendLine("    // Transform pixel position to light's clip space, then to UV space:");

		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			["    float4 shadowProj = mul(_light.mtxShadowWorld2Clip, worldPosBiased);"],   //bloody Metal, breaking matrix multiplication conventions.
			["    float4 shadowProj = _light.mtxShadowWorld2Clip * worldPosBiased;"],
			["    float4 shadowProj = mul(_light.mtxShadowWorld2Clip, worldPosBiased);"]);

		_ctx.functions
			.AppendLine("    shadowProj /= shadowProj.w;")
			.AppendLine("    float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;")
			.AppendLine()
			.AppendLine("    // Load corresponding depth value from shadow texture array:")
			.AppendLine("    half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, _light.shadowMapIdx));")
			.AppendLine("    half lightWeight = shadowDepth > shadowProj.z ? 1 : 0;")
			.AppendLine()
			.AppendLine("    // Fade shadows out near boundaries of UV/Depth space:")
			.AppendLine("    if (_light.lightType == 2)")
			.AppendLine("    {")
			.AppendLine("        half3 edgeUv = half3(shadowUv, shadowProj.z) * SHADOW_EDGE_FACE_SCALE;")
			.AppendLine("        half3 edgeMax = min(min(edgeUv, SHADOW_EDGE_FACE_SCALE - edgeUv), 1);")
			.AppendLine("        half k = 1 - min(min(edgeMax.x, edgeMax.y), edgeMax.z);")
			.AppendLine($"        lightWeight = {funcNameLerp}(lightWeight, 1.0, clamp(k, 0, 1));")
			.AppendLine("    }")
			.AppendLine("    return lightWeight;")
			.AppendLine("}")
			.AppendLine();

		_ctx.globalDeclarations.Add(nameFunc);
		return success;
	}

	private static bool WriteFunction_Lighting_CalculateTotalLightIntensity(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		const string nameFunc = "CalculateTotalLightIntensity";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		bool success = true;

		// Add all functions that may be referenced here-after immediately:
		if (_config.useAmbientLight)
		{
			success &= WriteFunction_Lighting_CalculateAmbientLight(in _ctx);
		}
		if (_config.useLightMaps)
		{
			success &= WriteFunction_Lighting_CalculateLightMaps(in _ctx);
		}
		if (_config.useLightSources)
		{
			success &= ShaderGenUniforms.WriteConstantBuffer_CBCamera(in _ctx);
			success &= WriteResource_Lighting_BufLights(in _ctx);
			success &= WriteFunction_Lighting_CalculatePhongLighting(in _ctx);

			if (_config.useShadowMaps)
			{
				success &= WriteResource_Lighting_TexShadowMaps(in _ctx);
				success &= WriteFunction_Lighting_CalculateShadowMapLightWeight(in _ctx);
			}
		}

		// Create header of main lighting function:
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			["half3 CalculateTotalLightIntensity(in float3 _worldPosition, in float3 _worldNormal)"],
			["half3 CalculateTotalLightIntensity(device const Light* BufLights, const float3& _worldPosition, const float3& _worldNormal)"],
			null);

		// Declare and initialize "totalLightIntensity" from zero or ambient light!
		_ctx.functions
			.AppendLine("{")
			.Append("    half3 totalLightIntensity = ");
		if (_config.useAmbientLight)
		{
			_ctx.functions.AppendLine($"CalculateAmbientLight(_worldNormal);");
		}
		else
		{
			_ctx.functions.AppendLine("half3(0, 0, 0);");
		}
		_ctx.functions.AppendLine();

		// Start first light source loop:
		string varNameLightCountFirst = _config.useShadowMaps
			? "shadowMappedLightCount"
			: "lightCount";

		_ctx.functions
			.AppendLine("    // Shadow-casting light sources:")
			.AppendLine($"    for (uint i = 0; i < {varNameLightCountFirst}; ++i)")
			.AppendLine("    {");

		if (_config.useShadowMaps)
		{
			success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
				["        Light light = BufLights[i];"],
				["        device const Light& light = *BufLights[i];"],
				null);
			_ctx.functions
				.AppendLine()
				.AppendLine("        half3 lightIntensity = CalculatePhongLighting(light, _worldPosition, _worldNormal);")
				.AppendLine("        half lightWeight = CalculateShadowMapLightWeight(light, _worldPosition, _worldNormal);")
				.AppendLine("        totalLightIntensity += lightIntensity * lightWeight;")
				.AppendLine("    }");
		}

		// Start non-casting light source loop:
		if (_config.useShadowMaps)
		{
			_ctx.functions
			.AppendLine("    // Simple light sources:")
			.AppendLine("    for (i = shadowMappedLightCount; i < lightCount; ++i)")
			.AppendLine("    {");
		}
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			["        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);"],
			["        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);"],
			null);
		_ctx.functions.AppendLine("    }");

		// Return result and end function:
		_ctx.functions.AppendLine("    return totalLightIntensity;");
		_ctx.functions.AppendLine("}");

		_ctx.globalDeclarations.Add(nameFunc);
		return success;
	}

	public static bool WriteVariable_Lighting(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		const string nameLocalVar = "totalLightIntensity";

		bool success = true;

		// REFERENCED FUNCTIONS:
		{
			if (_config.useLightMaps || _config.useLightSources)
			{
				_ctx.functions
					.AppendLine("/****************** LIGHTING: ******************/")
					.AppendLine();
			}

			success &= WriteFunction_Lighting_CalculateTotalLightIntensity(in _ctx, _config);
		}

		// MAIN CODE:
		foreach (ShaderGenVariant variant in _ctx.variants)
		{
			bool alreadyDeclared = variant.localDeclarations.Contains(nameLocalVar);

			string varNormalName = !string.IsNullOrEmpty(variant.varNameNormals)
				? variant.varNameNormals
				: "inputBasic.normal";

			// Declare "totalLightIntensity":
			variant.code.AppendLine($"    // Apply {_config.lightingModel} lighting:");
			if (alreadyDeclared)
			{
				variant.code.Append("    totalLightIntensity = ");
			}
			else
			{
				variant.code.Append("    half3 totalLightIntensity = ");
			}

			// If not using any further light sources:
			if (!_config.useLightMaps && !_config.useLightSources)
			{
				// Initialize "totalLightIntensity":
				if (_config.useAmbientLight)
				{
					variant.code.AppendLine($"CalculateAmbientLight(_worldNormal);");
				}
				else
				{
					variant.code.AppendLine("half3(0, 0, 0);");
				}
				variant.code.AppendLine();

				return true;
			}

			// Initialize "totalLightIntensity" from main lighting function:
			variant.code.AppendLine($"CalculateTotalLightIntensity(inputBasic.worldPosition, {varNormalName});");
			variant.code.AppendLine();

			if (!alreadyDeclared)
			{
				variant.localDeclarations.Add(nameLocalVar);
			}
		}

		return success;
	}

	public static bool ApplyLighting(in ShaderGenContext _ctx)
	{
		const string nameVarLightInt = "totalLightIntensity";
		const string nameVarAlbedo = "albedo";

		foreach (ShaderGenVariant variant in _ctx.variants)
		{
			if (!variant.localDeclarations.Contains(nameVarAlbedo) ||
				!variant.localDeclarations.Contains(nameVarLightInt))
			{
				Logger.Instance?.LogError($"Cannot add code to apply lighting, since local variables '{nameVarAlbedo}' or '{nameVarLightInt}' have not been declared!");
				return false;
			}

			variant.code.AppendLine("    albedo *= half4(totalLightIntensity, 1.0);");
		}
		return true;
	}

	#endregion
}
