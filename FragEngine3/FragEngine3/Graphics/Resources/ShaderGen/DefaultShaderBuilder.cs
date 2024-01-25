using FragEngine3.EngineCore;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public static class DefaultShaderBuilder
{
	#region Types

	private sealed class Context
	{
		public readonly StringBuilder constants = new(2300);
		public readonly StringBuilder resources = new(512);
		public readonly StringBuilder vertexOutputs = new(300);
		public readonly StringBuilder functions = new(4096);
		public readonly StringBuilder mainInputs = new(256);
		public readonly StringBuilder mainCode = new(650);
		public readonly StringBuilder mainHeader = new(512);
		//^Note: Starting capacities set for a pixel shader with all features enabled.

		public readonly HashSet<string> globalDeclarations = new(10);
		public MeshVertexDataFlags vertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
		public string varNameNormals = "inputBasic.normal";

		public int boundUniformsIdx = 3;
		public int boundTextureIdx = 2;
		public int boundBufferIdx = 0;
		public int boundSamplerIdx = 1;
	}

	#endregion
	#region Methods

	public static bool CreatePixelShader(EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		DefaultShaderConfig config = DefaultShaderConfig.ConfigWhiteLit;

		return CreatePixelShaderVariation(config, _platformFlags, out _outShaderCode);
	}

	public static bool CreatePixelShaderVariation(in DefaultShaderConfig _config, EnginePlatformFlag _platformFlags, out string _outShaderCode)
	{
		Context ctx = new();
		StringBuilder finalBuilder = new(4096);

		DefaultShaderLanguage language;
		if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_D3D))
			language = DefaultShaderLanguage.HLSL;
		else if (_platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Metal))
			language = DefaultShaderLanguage.Metal;
		else
			language = DefaultShaderLanguage.GLSL;

		// Add universal header defines and compiler instructions:
		WriteLanguageCodeLines(finalBuilder, language,
			[
				"#pragma pack_matrix( column_major )"
			],
			[
				"#include <metal_stdlib>",
				"using namespace metal;",
			],
			null);
		finalBuilder.AppendLine();

		AddAlbedo(in ctx, in _config, language);

		if (_config.useNormalMap)
		{
			AddNormalMaps(in ctx, in _config);
		}

		if (_config.applyLighting)
		{
			AddLighting(in ctx, in _config, language);
		}

		// Assemble full shader code file:
		AddHeader_MainPixel(ctx, ctx.mainHeader, language);
		finalBuilder.Append(ctx.constants).AppendLine();
		finalBuilder.Append(ctx.resources).AppendLine();
		finalBuilder.Append(ctx.vertexOutputs).AppendLine();
		finalBuilder.Append(ctx.functions).AppendLine();

		// Add entrypoint function:
		finalBuilder.Append(ctx.mainHeader);
		finalBuilder.Append(ctx.mainCode);
		AddFunctionEnd_MainPixel(finalBuilder);

		// Output resulting shader code:
		_outShaderCode = finalBuilder.ToString();
		return true;
	}

	private static void WriteColorValues(StringBuilder _dstBuilder, RgbaFloat _color)
	{
		_dstBuilder.Append(_color.R).Append(", ").Append(_color.G).Append(", ").Append(_color.B).Append(", ").Append(_color.A);
	}

	private static int WriteLanguageCodeLines(StringBuilder _dstBuilder, DefaultShaderLanguage _language, string[]? _codeHLSL, string[]? _codeMetal, string[]? _codeGLSL)
	{
		string[]? codeLines = _language switch
		{
			DefaultShaderLanguage.HLSL => _codeHLSL,
			DefaultShaderLanguage.Metal => _codeMetal,
			DefaultShaderLanguage.GLSL => _codeGLSL,
			_ => null,
		};

		if (codeLines != null && codeLines.Length != 0)
		{
			foreach (string line in codeLines)
			{
				_dstBuilder.AppendLine(line);
			}
			return codeLines.Length;
		}
		Logger.Instance?.LogError($"Code lines not currently available for shading language '{_language}'.");
		return 0;
	}
	
	private static bool WriteConstantBuffer_CBScene(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameConst = "CBScene";
		if (_ctx.globalDeclarations.Contains(nameConst)) return true;

		string typeNameVec = _language == DefaultShaderLanguage.GLSL
			? "vec4"
			: "float4";

		// Write structure header:
		_ctx.constants.AppendLine("// Constant buffer containing all scene-wide settings:");
		WriteLanguageCodeLines(_ctx.constants, _language,
			[ "cbuffer CBScene : register(b0)" ],
			[ "struct CBScene" ],
			null);

		// Write body:
		_ctx.constants
			.AppendLine("{")
			.AppendLine("    // Scene lighting:")
			.AppendLine($"    {typeNameVec} ambientLightLow;")
			.AppendLine($"    {typeNameVec} ambientLightMid;")
			.AppendLine($"    {typeNameVec} ambientLightHigh;")
			.AppendLine("    float shadowFadeStart;")
			.AppendLine("};");

		_ctx.globalDeclarations.Add(nameConst);
		return true;
	}

	private static bool WriteConstantBuffer_CBCamera(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameConst = "CBCamera";
		if (_ctx.globalDeclarations.Contains(nameConst)) return true;

		string typeNameMtx = _language == DefaultShaderLanguage.GLSL
			? "mat4"
			: "float4x4";
		string typeNameVec = _language == DefaultShaderLanguage.GLSL
			? "vec4"
			: "float4";

		// Write structure header:
		_ctx.constants.AppendLine("// Constant buffer containing all settings that apply for everything drawn by currently active camera:");
		WriteLanguageCodeLines(_ctx.constants, _language,
			[ "cbuffer CBCamera : register(b1)" ],
			[ "struct CBCamera" ],
			null);

		// Write body:
		_ctx.constants
			.AppendLine("{")
			.AppendLine("    // Camera vectors & matrices:")
			.AppendLine($"    {typeNameMtx} mtxWorld2Clip;")
			.AppendLine($"    {typeNameVec} cameraPosition;")
			.AppendLine($"    {typeNameVec} cameraDirection;")
			.AppendLine($"    {typeNameMtx} mtxCameraMotion;")
			.AppendLine()
			.AppendLine("    // Camera parameters:")
			.AppendLine("    uint cameraIdx;")
			.AppendLine("    uint resolutionX;")
			.AppendLine("    uint resolutionY;")
			.AppendLine("    float nearClipPlane;")
			.AppendLine("    float farClipPlane;")
			.AppendLine()
			.AppendLine("    // Per-camera lighting:")
			.AppendLine("    uint lightCount;")
			.AppendLine("    uint shadowMappedLightCount;")
			.AppendLine("};");

		_ctx.globalDeclarations.Add(nameConst);
		return true;
	}

	private static bool WriteVertexOutput_Basic(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameVert = "VertexOutput_Basic";
		if (_ctx.globalDeclarations.Contains(nameVert)) return true;

		// Write structure header:
		_ctx.vertexOutputs
			.AppendLine("struct VertexOutput_Basic")
			.AppendLine("{");

		// Write body:
		WriteLanguageCodeLines(_ctx.vertexOutputs, _language,
			// HLSL:
			[
				"    float4 position : SV_POSITION;",
				"    float3 worldPosition : COLOR0;",
				"    float3 normal : NORMAL0;",
				"    float2 uv : TEXCOORD0;"
			],
			// Metal:
			[
				"    float4 position; [[ position ]]",
				"    float3 worldPosition;",
				"    float3 normal;",
				"    float2 uv;"
			],
			// GLSL:
			null);
		_ctx.vertexOutputs.AppendLine("};");

		_ctx.globalDeclarations.Add(nameVert);
		return true;
	}

	private static bool WriteVertexOutput_Extended(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameVert = "VertexOutput_Extended";
		if (_ctx.globalDeclarations.Contains(nameVert)) return true;

		// Write structure header:
		_ctx.vertexOutputs
			.AppendLine("struct VertexOutput_Extended")
			.AppendLine("{");

		// Write body:
		WriteLanguageCodeLines(_ctx.vertexOutputs, _language,
			// HLSL:
			[
				"    float4 tangent : NORMAL1;",
				"    float3 binormal : NORMAL2;",
				"    float2 uv2 : TEXCOORD1;"
			],
			// Metal:
			[
				"    float4 tangent;",
				"    float3 binormal;",
				"    float2 uv2;"
			],
			// GLSL:
			null);
		_ctx.vertexOutputs.AppendLine("};");

		_ctx.globalDeclarations.Add(nameVert);
		return true;
	}

	private static bool AddAlbedo(in Context _ctx, in DefaultShaderConfig _config, DefaultShaderLanguage _language)
	{
		// Write basic declaration:
		_ctx.mainCode.AppendLine("    // Sample base color from main texture:");
		_ctx.mainCode.Append("    half4 albedo = ");

		// Initialize vector either from color literal or sample from main texture:
		switch (_language)
		{
			case DefaultShaderLanguage.HLSL:
				if (_config.albedoSource == DefaultShaderAlbedoSource.SampleTexMain)
				{
					// Declare resources TexMain and its sampler:
					string samplerMainName = !string.IsNullOrEmpty(_config.samplerTexMain)
						? _config.samplerTexMain
						: "SamplerMain";
					_ctx.resources.AppendLine("Texture2D<half4> TexMain : register(ps, t2);");
					_ctx.resources.AppendLine($"SamplerState {samplerMainName} : register(s1);");

					// Sample texture:
					_ctx.mainCode.AppendLine("TexMain.Sample(SamplerMain, inputBasic.uv);");

					// Increment slot indices for material-bound resources:
					_ctx.boundTextureIdx++;
					_ctx.boundSamplerIdx++;
				}
				else
				{
					_ctx.mainCode.Append($"half4(");
					WriteColorValues(_ctx.mainCode, _config.albedoColor);
					_ctx.mainCode.AppendLine(");");
				}
				break;
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_language}'.");
					return false;
				}
		}

		_ctx.mainCode.AppendLine();
		return true;
	}

	private static bool AddNormalMaps(in Context _ctx, in DefaultShaderConfig _config)
	{
		//TODO
		return true;
	}

	private static bool WriteType_Lighting_Light(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameType = "Light";
		if (_ctx.globalDeclarations.Contains(nameType)) return true;

		string typeNameVec = _language == DefaultShaderLanguage.GLSL
			? "vec3"
			: "float3";
		string typeNameMtx = _language == DefaultShaderLanguage.GLSL
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

	private static bool WriteResource_Lighting_BufLights(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameRes = "BufLights";
		if (_ctx.globalDeclarations.Contains(nameRes)) return true;

		// Ensure the light type is also defined:
		WriteType_Lighting_Light(in _ctx, _language);

		// Metal:
		if (_language == DefaultShaderLanguage.Metal)
		{
			_ctx.mainInputs.Append(", device const Light* BufLights [[ buffer( 3 ) ]]");
		}
		// HLSL & GLSL:
		else
		{
			_ctx.resources.AppendLine("StructuredBuffer<Light> BufLights : register(ps, t0);");
		}

		_ctx.globalDeclarations.Add(nameRes);
		return true;
	}

	private static bool WriteResource_Lighting_TexShadowMaps(in Context _ctx, DefaultShaderLanguage _language)
	{
		// TexShadowMaps:
		const string nameTex = "TexShadowMaps";
		if (!_ctx.globalDeclarations.Contains(nameTex))
		{
			// Metal:
			if (_language == DefaultShaderLanguage.Metal)
			{
				_ctx.mainInputs.Append(", texture2d<half, access::read> TexOpaqueColor [[ texture( 0 ) ]]");
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
			if (_language == DefaultShaderLanguage.Metal)
			{
				//TODO: no idea how this is done. This language is a convoluted mess.
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

	private static bool WriteFunction_Lighting_CalculateAmbientLight(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameFunc = "CalculateAmbientLight";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		WriteConstantBuffer_CBScene(in _ctx, _language);

		// Write function header:
		WriteLanguageCodeLines(_ctx.functions, _language,
			[ "half3 CalculateAmbientLight(in float3 _worldNormal)" ],
			[ "half3 CalculateAmbientLight(const float3& _worldNormal)" ],
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
		return true;
	}

	private static bool WriteFunction_Lighting_CalculateLightMaps(in Context _ctx, DefaultShaderLanguage _language)
	{
		//TODO
		return true;
	}

	private static bool WriteFunction_Lighting_CalculatePhongLighting(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameFunc = "CalculatePhongLighting";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		// Write function header:
		WriteLanguageCodeLines(_ctx.functions, _language,
			[ "half3 CalculatePhongLighting(in Light _light, in float3 _worldPosition, in float3 _worldNormal)" ],
			[ "half3 CalculatePhongLighting(device const Light& _light, const float3& _worldPosition, const float3& _worldNormal)" ],
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
		return true;
	}

	private static bool WriteFunction_Lighting_CalculateShadowMapLightWeight(in Context _ctx, DefaultShaderLanguage _language)
	{
		const string nameFunc = "CalculateShadowMapLightWeight";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		WriteResource_Lighting_TexShadowMaps(in _ctx, _language);

		// Write define for "SHADOW_EDGE_FACE_SCALE":
		_ctx.functions
			.AppendLine("#define SHADOW_EDGE_FACE_SCALE 10.0")
			.AppendLine();

		// Write function header:
		WriteLanguageCodeLines(_ctx.functions, _language,
			[ "half CalculateShadowMapLightWeight(in Light _light, in float3 _worldPosition, in float3 _worldNormal)" ],
			[ "half CalculateShadowMapLightWeight(device const Light& _light, const float3& _worldPosition, const float3& _worldNormal)" ],
			null);

		// Write function body:
		string funcNameLerp = _language == DefaultShaderLanguage.HLSL
			? "lerp"
			: "mix";

		_ctx.functions
			.AppendLine("{")
			.AppendLine("    // Add a bias to position along surface normal, to counter-act stair-stepping artifacts:")
			.AppendLine("    float4 worldPosBiased = float4(_worldPosition + _worldNormal * _light.shadowBias, 1);")
			.AppendLine()
			.AppendLine("    // Transform pixel position to light's clip space, then to UV space:");

		WriteLanguageCodeLines(_ctx.functions, _language,
			[ "    float4 shadowProj = mul(_light.mtxShadowWorld2Clip, worldPosBiased);" ],	//bloody Metal, breaking matrix multiplication conventions.
			[ "    float4 shadowProj = _light.mtxShadowWorld2Clip * worldPosBiased;" ],
			[ "    float4 shadowProj = mul(_light.mtxShadowWorld2Clip, worldPosBiased);" ]);

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
		return true;
	}

	private static bool AddLighting(in Context _ctx, in DefaultShaderConfig _config, DefaultShaderLanguage _language)
	{
		const string nameFunc = "CalculateTotalLightIntensity";
		if (_ctx.globalDeclarations.Contains(nameFunc)) return true;

		string varNormalName = !string.IsNullOrEmpty(_ctx.varNameNormals)
			? _ctx.varNameNormals
			: "inputBasic.normal";

		// REFERENCED FUNCTIONS:
		{
			if (_config.useLightMaps || _config.useLightSources)
			{
				_ctx.functions
					.AppendLine("/****************** LIGHTING: ******************/")
					.AppendLine();
			}

			// Add all functions that may be referenced here-after immediately:
			if (_config.useAmbientLight)
			{
				WriteFunction_Lighting_CalculateAmbientLight(in _ctx, _language);
			}
			if (_config.useLightMaps)
			{
				WriteFunction_Lighting_CalculateLightMaps(in _ctx, _language);
			}
			if (_config.useLightSources)
			{
				WriteConstantBuffer_CBCamera(in _ctx, _language);
				WriteResource_Lighting_BufLights(in _ctx, _language);
				WriteFunction_Lighting_CalculatePhongLighting(in _ctx, _language);

				if (_config.useShadowMaps)
				{
					WriteResource_Lighting_TexShadowMaps(in _ctx, _language);
					WriteFunction_Lighting_CalculateShadowMapLightWeight(in _ctx, _language);
				}
			}
		}

		// MAIN CODE:
		{
			// Declare "totalLightIntensity":
			_ctx.mainCode.AppendLine($"    // Apply {_config.lightingModel} lighting:");
			_ctx.mainCode.Append("    half3 totalLightIntensity = ");

			// If not using any further light sources:
			if (!_config.useLightMaps && !_config.useLightSources)
			{
				// Initialize "totalLightIntensity":
				if (_config.useAmbientLight)
				{
					_ctx.mainCode.AppendLine($"CalculateAmbientLight(_worldNormal);");
				}
				else
				{
					_ctx.mainCode.AppendLine("half3(0, 0, 0);");
				}
				_ctx.mainCode.AppendLine();

				return true;
			}

			// Initialize "totalLightIntensity" from main lighting function:
			_ctx.mainCode.AppendLine($"CalculateTotalLightIntensity(inputBasic.worldPosition, {varNormalName});");
			_ctx.mainCode.AppendLine();
		}

		// LIGHTING FUNCTION:
		{
			// Create header of main lighting function:
			WriteLanguageCodeLines(_ctx.functions, _language,
				[ "half3 CalculateTotalLightIntensity(in float3 _worldPosition, in float3 _worldNormal)" ],
				[ "half3 CalculateTotalLightIntensity(device const Light* BufLights, const float3& _worldPosition, const float3& _worldNormal)" ],
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
				WriteLanguageCodeLines(_ctx.functions, _language,
					[ "        Light light = BufLights[i];" ],
					[ "        device const Light& light = *BufLights[i];" ],
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
			WriteLanguageCodeLines(_ctx.functions, _language,
				["        totalLightIntensity += CalculatePhongLighting(BufLights[i], _worldPosition, _worldNormal);"],
				null,
				null);
			_ctx.functions.AppendLine("    }");

			// Return result and end function:
			_ctx.functions.AppendLine("    return totalLightIntensity;");
			_ctx.functions.AppendLine("}");
		}

		_ctx.globalDeclarations.Add(nameFunc);
		return true;
	}

	private static bool AddHeader_MainPixel(in Context _ctx, StringBuilder _finalBuilder, DefaultShaderLanguage _language)
	{
		_finalBuilder
			.AppendLine("/******************* SHADERS: ******************/")
			.AppendLine();

		bool hasExtendedData = _ctx.vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
		bool hasBlendShapes = _ctx.vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
		bool hasBoneAnimation = _ctx.vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations);

		// Ensure the required vertex output types are defined:
		WriteVertexOutput_Basic(in _ctx, _language);
		if (hasExtendedData) WriteVertexOutput_Extended(in _ctx, _language);

		// Write function's base name:
		_finalBuilder.Append("half4 Main_Pixel");

		// Add vertex flags to name:
		if (hasExtendedData) _finalBuilder.Append('_').Append(ExtendedVertex.shaderEntryPointSuffix);
		if (hasBlendShapes) _finalBuilder.Append('_').Append(IndexedWeightedVertex.shaderEntryPointSuffix_Blend);
		if (hasBoneAnimation) _finalBuilder.Append('_').Append(IndexedWeightedVertex.shaderEntryPointSuffix_Anim);

		// List vertex input paramaters:
		switch (_language)
		{
			case DefaultShaderLanguage.HLSL:
				{
					_finalBuilder.Append($"(in {BasicVertex.shaderVertexOuputName} inputBasic");

					if (hasExtendedData) _finalBuilder.Append($", in {ExtendedVertex.shaderVertexOuputName} inputExt");
					if (hasBlendShapes) _finalBuilder.Append($", in {IndexedWeightedVertex.shaderVertexOuputName_Blend} inputExt");
					if (hasBoneAnimation) _finalBuilder.Append($", in {IndexedWeightedVertex.shaderVertexOuputName_Anim} inputExt");
				}
				break;
			//...
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_language}'.");
					return false;
				}
		}

		// List any further input paramaters from other features: (mostly needed for Metal)
		if (_ctx.mainInputs.Length != 0)
		{
			_finalBuilder.Append(", ").Append(_ctx.mainInputs);
		}

		// Close function header:
		switch (_language)
		{
			case DefaultShaderLanguage.HLSL:
				{
					_finalBuilder.AppendLine(") : SV_Target0").AppendLine("{");
				}
				break;
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_language}'.");
					return false;
				}
		}
		return true;
	}
	private static bool AddFunctionEnd_MainPixel(StringBuilder _finalBuilder)
	{
		_finalBuilder.AppendLine(
			"    // Return final color:\n" +
			"    return albedo;\n" +
			"};");
		return true;
	}

	#endregion
}
