namespace FragEngine3.Graphics.Resources.ShaderGen.SampleCode;

public static class PixelShaderSamples
{
	#region Constants

	// DEFINES:

	public const string name_fileStart = "FileStart_PS";
	private const string code_fileStart = "#pragma pack_matrix( column_major )\r\n";

	// ENTRYPOINTS:

	public const string name_functionStart = "Main_Pixel";
	private const string code_functionStart_MainPixel =
		"half4 Main_Pixel(in VertexOutput_Basic inputBasic) : SV_Target0\r\n" +
		"{\r\n";

	private const string code_functionStart_MainPixel_Ext =
		"half4 Main_Pixel_Ext(in VertexOutput_Basic inputBasic, in VertexOutput_Extended inputExt) : SV_Target0\r\n" +
		"{\r\n";

	public const string name_functionEnd = "Main_Pixel_End";
	private const string code_functionEnd_MainPixel =
		"\t// Return final color:\r\n" +
		"\treturn albedo;\r\n" +
		"};\r\n";

	// CONSTANT BUFFERS:

	// Scene constants:
	public const string name_type_cbScene = "CBScene";
	private const string code_type_cbScene =
		"\r\n// Constant buffer containing all scene-wide settings:\r\n" +
		"cbuffer CBScene : register(b0)\r\n" +
		"{\r\n" +
		"    // Scene lighting:\r\n" +
		"    float4 ambientLightLow;\r\n" +
		"    float4 ambientLightMid;\r\n" +
		"    float4 ambientLightHigh;\r\n" +
		"    float shadowFadeStart;\r\n" +
		"};\r\n";

	// Camera constants:
	public const string name_type_cbCamera = "CBCamera";
	public const string code_type_cbCamera =
		"\r\n// Constant buffer containing all settings that apply for everything drawn by currently active camera:\r\n" +
		"cbuffer CBCamera : register(b1)\r\n" +
		"{\r\n" +
		"    // Camera vectors & matrices:\r\n" +
		"    float4x4 mtxWorld2Clip;\r\n" +
		"    float4 cameraPosition;\r\n" +
		"    float4 cameraDirection;\r\n" +
		"    float4x4 mtxCameraMotion;\r\n" +
		"\r\n" +
		"    // Camera parameters:\r\n" +
		"    uint cameraIdx;\r\n" +
		"    uint resolutionX;\r\n" +
		"    uint resolutionY;\r\n" +
		"    float nearClipPlane;\r\n" +
		"    float farClipPlane;\r\n" +
		"\r\n" +
		"    // Per-camera lighting:\r\n" +
		"    uint lightCount;\r\n" +
		"    uint shadowMappedLightCount;\r\n" +
		"};\r\n";

	// Object constants:
	public const string name_type_cbObject = "CBObject";
	private const string code_type_cbObject =
		"\r\n// Constant buffer containing only object-specific settings:\r\n" +
		"cbuffer CBObject : register(b2)\r\n" +
		"{\r\n" +
		"    float4x4 mtxLocal2World;\r\n" +
		"    float3 worldPosition;\r\n" +
		"    float boundingRadius;\r\n" +
		"};\r\n";

	public const string name_feat_defaultConstantBuffers = "DefaultConstantBuffers";

	// MAIN INPUTS:

	// Basic vertex data:
	public const string name_var_inputBasic_fragmentPosition = "inputBasic.position";
	public const string name_var_inputBasic_worldPosition = "inputBasic.worldPosition";
	public const string name_var_inputBasic_normal = "inputBasic.normal";
	public const string name_var_inputBasic_uv = "inputBasic.uv";

	public const string name_type_inputBasic = "VertexOutput_Basic";
	private const string code_type_inputBasic =
		"\r\nstruct VertexOutput_Basic\r\n" +
		"{\r\n" +
		"    float4 position : SV_POSITION;\r\n" +
		"    float3 worldPosition : COLOR0;\r\n" +
		"    float3 normal : NORMAL0;\r\n" +
		"    float2 uv : TEXCOORD0;\r\n" +
		"};\r\n";
	
	// Extended vertex data:
	public const string name_var_inputExt_tangent = "inputExt.tangent";
	public const string name_var_inputExt_binormal = "inputExt.binormal";
	public const string name_var_inputExt_uv2 = "inputExt.uv2";

	public const string name_type_inputExt = "VertexOutput_Extended";
	private const string code_type_inputExt =
		"\r\nstruct VertexOutput_Extended\r\n" +
		"{\r\n" +
		"    float3 tangent : NORMAL1;\r\n" +
		"    float3 binormal : NORMAL2;\r\n" +
		"    float2 uv2 : TEXCOORD1;\r\n" +
		"};\r\n";

	// BASE ALBEDO:

	public const string name_var_albedo = "albedo";
	private const string code_var_albedo_white = "\thalf4 albedo = half4(1, 1, 1, 1);\r\n";
	private const string code_var_albedo_zero = "\thalf4 albedo = half4(0, 0, 0, 0);\r\n";
	private const string code_var_albedo_sampleTexMain = "\thalf4 albedo = TexMain.Sample(SamplerMain, inputBasic.uv);\r\n";

	// RESOURCES:

	public const string name_res_texMain = "TexMain";
	private const string code_res_texMain = "Texture2D<half4> TexMain : register(ps, t2);\r\n";

	public const string name_res_samplerMain = "SamplerMain";
	private const string code_res_samplerMain = "SamplerState SamplerMain : register(s1);\r\n";

	#endregion
	#region Methods

	public static ShaderGenCodeDeclaration CreateFileStart_PS() => new()
	{
		Name = "FileStart_PS",
		TemplateNames = null,
		Code = code_fileStart,
	};

	public static ShaderGenCodeDeclaration CreateType_CBScene() => new()
	{
		Name = name_type_cbScene,
		Code = code_type_cbScene,
	};
	public static ShaderGenCodeDeclaration CreateType_CBCamera() => new()
	{
		Name = name_type_cbCamera,
		Code = code_type_cbCamera,
	};
	public static ShaderGenCodeDeclaration CreateType_CBObject() => new()
	{
		Name = name_type_cbObject,
		Code = code_type_cbObject,
	};

	public static ShaderGenFeature CreateFeature_CBScene() => new()
	{
		Name = name_type_cbScene,
		
		// Declarations:
		TypesCode =
		[
			CreateType_CBScene(),
		],
		// Variables:
		Outputs =
		[
			ShaderGenVariable.CreateVector("ambientLightLow", ShaderGenBaseDataType.Float, 4, false),
			ShaderGenVariable.CreateVector("ambientLightMid", ShaderGenBaseDataType.Float, 4, false),
			ShaderGenVariable.CreateVector("ambientLightHigh", ShaderGenBaseDataType.Float, 4, false),
			ShaderGenVariable.CreateScalar("shadowFadeStart", ShaderGenBaseDataType.Float, false),
		],
		// Requirements:
		Dependencies =
		[
			// Functional:
			name_fileStart,
			name_functionStart,
		],
	};
	public static ShaderGenFeature CreateFeature_CBCamera() => new()
	{
		Name = name_type_cbCamera,

		// Declarations:
		TypesCode =
		[
			CreateType_CBCamera(),
		],
		// Variables:
		Outputs =
		[
			// Camera vectors & matrices:
			ShaderGenVariable.CreateMatrix("mtxWorld2Clip", ShaderGenBaseDataType.Float, 4, 4, false),
			ShaderGenVariable.CreateVector("cameraPosition", ShaderGenBaseDataType.Float, 4, false),
			ShaderGenVariable.CreateVector("cameraDirection", ShaderGenBaseDataType.Float, 4, false),
			ShaderGenVariable.CreateMatrix("mtxCameraMotion", ShaderGenBaseDataType.Float, 4, 4, false),

			// Camera parameters:
			ShaderGenVariable.CreateScalar("cameraIdx", ShaderGenBaseDataType.UInt, false),
			ShaderGenVariable.CreateScalar("resolutionX", ShaderGenBaseDataType.UInt, false),
			ShaderGenVariable.CreateScalar("resolutionY", ShaderGenBaseDataType.UInt, false),
			ShaderGenVariable.CreateScalar("nearClipPlane", ShaderGenBaseDataType.Float, false),
			ShaderGenVariable.CreateScalar("farClipPlane", ShaderGenBaseDataType.Float, false),

			// Per-camera lighting:
			ShaderGenVariable.CreateScalar("lightCount", ShaderGenBaseDataType.UInt, false),
			ShaderGenVariable.CreateScalar("shadowMappedLightCount", ShaderGenBaseDataType.UInt, false),
		],
		// Requirements:
		Dependencies =
		[
			// Functional:
			name_fileStart,
			name_functionStart,
		],
	};
	public static ShaderGenFeature CreateFeature_CBObject() => new()
	{
		Name = name_type_cbObject,

		// Declarations:
		TypesCode =
		[
			CreateType_CBObject(),
		],
		// Variables:
		Outputs =
		[
			ShaderGenVariable.CreateMatrix("mtxLocal2World", ShaderGenBaseDataType.Float, 4, 4, false),
			ShaderGenVariable.CreateVector("worldPosition", ShaderGenBaseDataType.Float, 3, false),
			ShaderGenVariable.CreateScalar("boundingRadius", ShaderGenBaseDataType.Float, false),
		],
		// Requirements:
		Dependencies =
		[
			// Functional:
			name_fileStart,
			name_functionStart,
		],
	};

	public static ShaderGenFeature CreateFeature_DefaultConstantBuffers() => new()
	{
		Name = name_feat_defaultConstantBuffers,
		
		// Requirements:
		Dependencies =
		[
			name_type_cbScene,
			name_type_cbCamera,
			name_type_cbObject,
		],
	};

	public static ShaderGenVariable CreateVariable_InputBasic(ShaderGenInputBasicPS _inputValue, bool _isMutable = true)
	{
		return _inputValue switch
		{
			ShaderGenInputBasicPS.FragmentPosition =>	ShaderGenVariable.CreateVector(name_var_inputBasic_fragmentPosition, ShaderGenBaseDataType.Float, 4, _isMutable),
			ShaderGenInputBasicPS.WorldPosition =>		ShaderGenVariable.CreateVector(name_var_inputBasic_worldPosition, ShaderGenBaseDataType.Float, 3, _isMutable),
			ShaderGenInputBasicPS.Normal =>				ShaderGenVariable.CreateVector(name_var_inputBasic_normal, ShaderGenBaseDataType.Float, 3, _isMutable),
			ShaderGenInputBasicPS.UV =>					ShaderGenVariable.CreateVector(name_var_inputBasic_fragmentPosition, ShaderGenBaseDataType.Float, 2, _isMutable),
			_ =>										ShaderGenVariable.None,
		};
	}
	public static ShaderGenVariable CreateVariable_InputExt(ShaderGenInputExtPS _inputValue, bool _isMutable = true)
	{
		return _inputValue switch
		{
			ShaderGenInputExtPS.Tangent =>	ShaderGenVariable.CreateVector(name_var_inputExt_tangent, ShaderGenBaseDataType.Float, 3, _isMutable),
			ShaderGenInputExtPS.Binormal =>	ShaderGenVariable.CreateVector(name_var_inputExt_binormal, ShaderGenBaseDataType.Float, 3, _isMutable),
			ShaderGenInputExtPS.UV2 =>		ShaderGenVariable.CreateVector(name_var_inputExt_uv2, ShaderGenBaseDataType.Float, 2, _isMutable),
			_ =>							ShaderGenVariable.None,
		};
	}

	public static ShaderGenCodeDeclaration CreateType_InputBasic() => new()
	{
		Name = name_type_inputBasic,
		Code = code_type_inputBasic,
	};
	public static ShaderGenCodeDeclaration CreateType_InputExt() => new()
	{
		Name = name_type_inputExt,
		Code = code_type_inputExt,
	};

	public static ShaderGenFeature CreateFunctionStart_PS(MeshVertexDataFlags _vertexFlags)
	{
		_vertexFlags |= MeshVertexDataFlags.BasicSurfaceData;
		bool useExtendedData = _vertexFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);

		return new ShaderGenFeature()
		{
			// Identifiers:
			Name = name_functionStart,

			// Declarations:
			TypesCode = useExtendedData
				? [
					CreateFileStart_PS(),
					CreateType_InputBasic(),
					CreateType_InputExt(),
				]
				: [
					CreateFileStart_PS(),
					CreateType_InputBasic(),
				],
			InsertCode = new ShaderGenCodeDeclaration()
			{
				Name = name_functionStart,
				Code = useExtendedData
					? code_functionStart_MainPixel_Ext
					: code_functionStart_MainPixel,
			},

			// Variables:
			Outputs = useExtendedData
				? [
					//Basic:
					CreateVariable_InputBasic(ShaderGenInputBasicPS.FragmentPosition),
					CreateVariable_InputBasic(ShaderGenInputBasicPS.WorldPosition),
					CreateVariable_InputBasic(ShaderGenInputBasicPS.Normal),
					CreateVariable_InputBasic(ShaderGenInputBasicPS.UV),
					//Extended:
					CreateVariable_InputExt(ShaderGenInputExtPS.Tangent),
					CreateVariable_InputExt(ShaderGenInputExtPS.Binormal),
					CreateVariable_InputExt(ShaderGenInputExtPS.UV2),
				]
				: [
					//Basic:
					CreateVariable_InputBasic(ShaderGenInputBasicPS.FragmentPosition),
					CreateVariable_InputBasic(ShaderGenInputBasicPS.WorldPosition),
					CreateVariable_InputBasic(ShaderGenInputBasicPS.Normal),
					CreateVariable_InputBasic(ShaderGenInputBasicPS.UV),
				],

			// Requirements:
			RequiredVertexFlags = _vertexFlags,
		};
	}

	public static ShaderGenFeature CreateFunctionEnd_PS()
	{
		return new ShaderGenFeature()
		{
			// Identifiers:
			Name = name_functionEnd,

			// Declarations:
			InsertCode = new ShaderGenCodeDeclaration()
			{
				Name = name_functionStart,
				Code = code_functionEnd_MainPixel,
			},

			// Requirements:
			Dependencies =
			[
				name_functionStart,
				name_var_albedo,
			],
		};
	}

	public static ShaderGenVariable CreateVariable_Albedo() => new()
	{
		Name = name_var_albedo,
		BaseType = ShaderGenBaseDataType.Half,
		SizeX = 4,
		SizeY = 1,
		IsMutable = true,
	};

	public static ShaderGenCodeDeclaration CreateDeclaration_Res_TexMain() => new()
	{
		Name = name_res_texMain,
		Code = code_res_texMain,
	};
	public static ShaderGenCodeDeclaration CreateDeclaration_Res_SamplerMain() => new()
	{
		Name = name_res_samplerMain,
		Code = code_res_samplerMain,
	};

	public static ShaderGenFeature CreateVariable_Albedo_InitializeColor(ShaderGenCastPadding _fill)
	{
		return new ShaderGenFeature()
		{
			// Identifiers:
			Name = name_var_albedo,

			// Declarations:
			InsertCode = new ShaderGenCodeDeclaration()
			{
				Name = name_var_albedo,
				Code = _fill == ShaderGenCastPadding.OneOrTrue
					? code_var_albedo_white             // half4 albedo = half4(1, 1, 1, 1);
					: code_var_albedo_zero,             // half4 albedo = half4(0, 0, 0, 0);
			},

			// Variables:
			Outputs =
			[
				CreateVariable_Albedo(),				// half4 albedo
			],
			
			// Requirements:
			Dependencies =
			[
				name_functionStart,
			],
		};
	}
	public static ShaderGenFeature CreateVariable_Albedo_InitializeSampleTexMain()
	{
		return new ShaderGenFeature()
		{
			// Identifiers:
			Name = name_var_albedo,

			// Declarations:
			TypesCode =
			[
				CreateDeclaration_Res_TexMain(),		// TexMain
				CreateDeclaration_Res_SamplerMain(),	// SamplerMain
			],
			InsertCode = new ShaderGenCodeDeclaration()
			{
				Name = name_var_albedo,
				Code = code_var_albedo_sampleTexMain,   // half4 albedo = TexMain.Sample(SamplerMain, inputBasic.uv);
			},

			// Variables:
			Inputs =
			[
				CreateVariable_InputBasic(ShaderGenInputBasicPS.UV, false),
			],
			Outputs =
			[
				CreateVariable_Albedo(),				// half4 albedo
			],

			// Requirements:
			Dependencies =
			[
				name_functionStart,
			],
		};
	}

	#endregion
}
