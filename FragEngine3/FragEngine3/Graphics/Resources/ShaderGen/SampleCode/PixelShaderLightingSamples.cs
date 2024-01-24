using FragEngine3.Graphics.Components;
using System;

namespace FragEngine3.Graphics.Resources.ShaderGen.SampleCode;

public static class PixelShaderLightingSamples
{
	#region Constants

	// FUNCTIONS:

	public const string name_func_ambientLight = "CalculateAmbientLight";
	private const string code_func_ambientLight =
		"\r\nhalf3 CalculateAmbientLight(in float3 _worldNormal)\r\n" +
		"{\r\n" +
		"    half dotY = (half)dot(_worldNormal, float3(0, 1, 0));\r\n" +
		"    half wLow = max(-dotY, 0);\r\n" +
		"    half wHigh = max(dotY, 0);\r\n" +
		"    half wMid = 1.0 - wHigh - wLow;\r\n" +
		"    return (wLow * (half4)ambientLightLow + wHigh * (half4)ambientLightHigh + wMid * (half4)ambientLightMid).xyz;\r\n" +
		"}\r\n";

	public const string name_func_phongLighting = "CalculatePhongLighting";
	private const string code_func_phongLighting =
		"\r\nhalf3 CalculatePhongLighting(in Light _light, in float3 _worldPosition, in float3 _worldNormal)\r\n" +
		"{\r\n" +
		"    half3 lightIntens = (half3)(_light.lightColor * _light.lightIntensity);\r\n" +
		"    float3 lightRayDir;\r\n" +
		"\r\n" +
		"    // Directional light:\r\n" +
		"    if (_light.lightType == 2)\r\n" +
		"    {\r\n" +
		"        lightRayDir = _light.lightDirection;\r\n" +
		"    }\r\n" +
		"    // Point or Spot light:\r\n" +
		"    else\r\n" +
		"    {\r\n" +
		"        float3 lightOffset = _worldPosition - _light.lightPosition;\r\n" +
		"        lightIntens /= (half)dot(lightOffset, lightOffset);\r\n" +
		"        lightRayDir = normalize(lightOffset);\r\n" +
		"\r\n" +
		"        // Spot light angle:\r\n" +
		"        if (_light.lightType == 1 && dot(_light.lightDirection, lightRayDir) < _light.lightSpotMinDot)\r\n" +
		"        {\r\n" +
		"            lightIntens = half3(0, 0, 0);\r\n" +
		"        }\r\n" +
		"    }\r\n" +
		"\r\n" +
		"    half lightDot = max(-(half)dot(lightRayDir, _worldNormal), 0.0);\r\n" +
		"    return lightIntens.xyz * lightDot;\r\n" +
		"}\r\n";

	public const string name_func_calcShadowMaps = "CalculateShadowMapLightWeight";
	private const string code_func_calcShadowMaps =
		"\r\n#define SHADOW_EDGE_FACE_SCALE 10.0\r\n" +
		"\r\n" +
		"half CalculateShadowMapLightWeight(in Light _light, in float3 _worldPosition, in float3 _worldNormal)\r\n" +
		"{\r\n" +
		"    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:\r\n" +
		"    float4 worldPosBiased = float4(_worldPosition + _worldNormal * _light.shadowBias, 1);\r\n" +
		"\r\n" +
		"    // Transform pixel position to light's clip space, then to UV space:\r\n" +
		"    float4 shadowProj = mul(_light.mtxShadowWorld2Clip, worldPosBiased);\r\n" +
		"    shadowProj /= shadowProj.w;\r\n" +
		"    float2 shadowUv = float2(shadowProj.x + 1, 1 - shadowProj.y) * 0.5;\r\n" +
		"\r\n" +
		"    // Load corresponding depth value from shadow texture array:\r\n" +
		"    half shadowDepth = TexShadowMaps.Sample(SamplerShadowMaps, float3(shadowUv.x, shadowUv.y, _light.shadowMapIdx));\r\n" +
		"    half lightWeight = shadowDepth > shadowProj.z ? 1 : 0;\r\n" +
		"\r\n" +
		"    // Fade shadows out near boundaries of UV/Depth space:\r\n" +
		"    if (_light.lightType == 2)\r\n" +
		"    {\r\n" +
		"        half3 edgeUv = half3(shadowUv, shadowProj.z) * SHADOW_EDGE_FACE_SCALE;\r\n" +
		"        half3 edgeMax = min(min(edgeUv, SHADOW_EDGE_FACE_SCALE - edgeUv), 1);\r\n" +
		"        half k = 1 - min(min(edgeMax.x, edgeMax.y), edgeMax.z);\r\n" +
		"        lightWeight = lerp(lightWeight, 1.0, clamp(k, 0, 1));\r\n" +
		"    }\r\n" +
		"    return lightWeight;\r\n" +
		"}\r\n";

	public const string name_func_totalLighting = "CalculateTotalLightIntensity";
	private const string code_func_totalLighting_simple =
		"\r\nhalf3 CalculateTotalLightIntensity(in float3 _worldPosition, in float3 _worldNormal)\r\n" +
		"{\r\n" +
		"    half3 totalLightIntensity = CalculateAmbientLight(_worldNormal);\r\n" +
		"\r\n" +
		"    // Simple light sources:\r\n" +
		"    for (uint i = 0; i < lightCount; ++i)\r\n" +
		"    {\r\n" +
		"        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);\r\n" +
		"    }\r\n" +
		"    return totalLightIntensity;\r\n" +
		"}\r\n";
	private const string code_func_totalLighting_shadowMapped =
		"\r\nhalf3 CalculateTotalLightIntensity(in float3 _worldPosition, in float3 _worldNormal)\r\n" +
		"{\r\n" +
		"    half3 totalLightIntensity = CalculateAmbientLight(_worldNormal);\r\n" +
		"\r\n" +
		"    // Shadow-casting light sources:\r\n" +
		"    for (uint i = 0; i < shadowMappedLightCount; ++i)\r\n" +
		"    {\r\n" +
		"        Light light = BufLights[i];\r\n" +
		"\r\n" +
		"        half3 lightIntensity = CalculatePhongLighting(light, _worldPosition, _worldNormal);\r\n" +
		"        half lightWeight = CalculateShadowMapLightWeight(light, _worldPosition, _worldNormal);\r\n" +
		"        totalLightIntensity += lightIntensity * lightWeight;\r\n" +
		"    }\r\n" +
		"    // Simple light sources:\r\n" +
		"    for (i = shadowMappedLightCount; i < lightCount; ++i)\r\n" +
		"    {\r\n" +
		"        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);\r\n" +
		"    }\r\n" +
		"    return totalLightIntensity;\r\n" +
		"}\r\n";

	public const string name_feat_applyLighting = "totalLightIntensity";
	private const string code_feat_applyLighting =
		"    // Apply basic phong lighting:\r\n" +
		"    half3 totalLightIntensity = CalculateTotalLightIntensity(inputBasic.worldPosition, inputBasic.normal);\r\n";

	private const string name_var_totalLightIntensity = "totalLightIntensity";

	// RESOURCES:

	public const string name_res_bufLights = "BufLights";
	private const string code_res_bufLights =
		"\r\nstruct Light\r\n" +
		"{\r\n" +
		"    float3 lightColor;\r\n" +
		"    float lightIntensity;\r\n" +
		"    float3 lightPosition;\r\n" +
		"    uint lightType;\r\n" +
		"    float3 lightDirection;\r\n" +
		"    float lightSpotMinDot;\r\n" +
		"    float4x4 mtxShadowWorld2Clip;\r\n" +
		"    uint shadowMapIdx;\r\n" +
		"    float shadowBias;\r\n" +
		"};\r\n" +
		"\r\n" +
		"StructuredBuffer<Light> BufLights : register(ps, t0);\r\n";

	#endregion
	#region Methods

	public static ShaderGenCodeDeclaration CreateFunction_AmbientLight() => new()
	{
		Name = name_func_ambientLight,
		Code = code_func_ambientLight,
	};
	public static ShaderGenCodeDeclaration CreateFunction_PhongLighting() => new()
	{
		Name = name_func_phongLighting,
		Code = code_func_phongLighting,
	};
	public static ShaderGenCodeDeclaration CreateFunction_CalcShadowMapLightWeight() => new()
	{
		Name = name_func_calcShadowMaps,
		Code = code_func_calcShadowMaps,
	};
	public static ShaderGenCodeDeclaration CreateFunction_CalcTotalLighting(bool _useShadowMaps)
	{
		return _useShadowMaps
			? new()
			{
				Name = name_func_totalLighting,
				Code = code_func_totalLighting_shadowMapped,
			}
			: new()
			{
				Name = name_func_totalLighting,
				Code = code_func_totalLighting_simple,
			};
	}

	public static ShaderGenCodeDeclaration CreateTypeAndResource_BufLights() => new()
	{
		Name = name_res_bufLights,
		Code = code_res_bufLights,
	};

	public static ShaderGenFeature CreateFeature_AmbientLight() => new()
	{
		Name = name_func_ambientLight,
		
		// Declarations:
		FunctionsCode =
		[
			CreateFunction_AmbientLight(),
		],
		// Requirements:
		Dependencies =
		[
			PixelShaderSamples.name_type_cbScene,
		],
	};

	public static ShaderGenFeature CreateFeature_PhongLighting() => new()
	{
		Name = name_func_phongLighting,

		// Declarations:
		TypesCode =
		[
			CreateTypeAndResource_BufLights(),
		],
		FunctionsCode =
		[
			CreateFunction_PhongLighting(),
		],
		// Requirements:
		Dependencies =
		[
			PixelShaderSamples.name_type_cbScene,
		],
	};

	public static ShaderGenFeature CreateFeature_CalcShadowMapLightWeight() => new()
	{
		Name = name_func_calcShadowMaps,

		// Declarations:
		TypesCode =
		[
			CreateTypeAndResource_BufLights(),
		],
		FunctionsCode =
		[
			CreateFunction_CalcShadowMapLightWeight(),
		],
		// Variables:
		Outputs =
		[
			ShaderGenVariable.CreateScalar("SHADOW_EDGE_FACE_SCALE", ShaderGenBaseDataType.Float, false),
		],
		// Requirements:
		Dependencies =
		[
			PixelShaderSamples.name_fileStart,
			PixelShaderSamples.name_type_cbScene,
		],
	};

	public static ShaderGenFeature CreateFeature_CalcTotalLighting(bool _useShadowMaps) => new()
	{
		Name = name_func_totalLighting,
		
		// Declarations:
		TypesCode =
		[
			CreateTypeAndResource_BufLights(),
		],
		FunctionsCode =
		[
			CreateFunction_CalcTotalLighting(_useShadowMaps),
		],
		// Requirements:
		Dependencies = _useShadowMaps
			? [
				name_func_ambientLight,
				name_func_phongLighting,
				name_func_calcShadowMaps,
			]
			: [
				name_func_ambientLight,
				name_func_phongLighting,
			],
	};

	public static ShaderGenVariable CreateVariable_TotalLightIntensity() => ShaderGenVariable.CreateVector(name_var_totalLightIntensity, ShaderGenBaseDataType.Half, 4, true);

	public static ShaderGenFeature CreateVariable_TotalLightIntensity_InitializeCalculateAll(bool _useShadowMaps) => new()
	{
		Name = name_feat_applyLighting,
		
		// Declarations:
		TypesCode =
		[
			CreateTypeAndResource_BufLights(),
		],
		FunctionsCode = _useShadowMaps
			? [
				CreateFunction_AmbientLight(),
				CreateFunction_PhongLighting(),
				CreateFunction_CalcShadowMapLightWeight(),
				CreateFunction_CalcTotalLighting(true),
			]
			: [
				CreateFunction_AmbientLight(),
				CreateFunction_PhongLighting(),
				CreateFunction_CalcTotalLighting(false),
			],
		// Variables:
		Inputs =
		[
			PixelShaderSamples.CreateVariable_InputBasic(ShaderGenInputBasicPS.WorldPosition, false),
			PixelShaderSamples.CreateVariable_InputBasic(ShaderGenInputBasicPS.Normal, false),
		],
		Outputs = _useShadowMaps
			? [
				ShaderGenVariable.CreateScalar("SHADOW_EDGE_FACE_SCALE", ShaderGenBaseDataType.Float, false),
				CreateVariable_TotalLightIntensity(),
			]
			: [
				CreateVariable_TotalLightIntensity(),
			],
		// Requirements:
		RequiredVertexFlags = MeshVertexDataFlags.BasicSurfaceData,
		Dependencies =
		[
			PixelShaderSamples.name_type_cbScene,
			PixelShaderSamples.name_functionStart,
		],
	};

	#endregion
}
