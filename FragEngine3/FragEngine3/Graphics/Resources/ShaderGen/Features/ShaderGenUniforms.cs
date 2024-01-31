namespace FragEngine3.Graphics.Resources.ShaderGen.Features;

public static class ShaderGenUniforms
{
	#region Methods

	private static void WriteResourceForMetal(in ShaderGenContext _ctx, string _code)
	{
		if (_ctx.language != ShaderGenLanguage.Metal) return;
		
		if (_ctx.resources.Length != 0)
		{
			_ctx.resources.Append(", ");
		}
		_ctx.resources.Append(_code);
	}

	public static bool WriteConstantBuffer_CBScene(in ShaderGenContext _ctx)
	{
		const string nameConst = "CBScene";
		if (_ctx.globalDeclarations.Contains(nameConst)) return true;

		bool success = true;

		string typeNameVec = _ctx.language == ShaderGenLanguage.GLSL
			? "vec4"
			: "float4";

		// Write structure header:
		_ctx.constants.AppendLine("// Constant buffer containing all scene-wide settings:");
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.constants, _ctx.language,
			[ "cbuffer CBScene : register(b0)" ],
			[ "struct CBScene" ],
			[ "layout (binding = 0) uniform CBScene" ]);

		// Write body:
		_ctx.constants
			.AppendLine("{")
			.AppendLine("    // Scene lighting:")
			.AppendLine($"    {typeNameVec} ambientLightLow;")
			.AppendLine($"    {typeNameVec} ambientLightMid;")
			.AppendLine($"    {typeNameVec} ambientLightHigh;")
			.AppendLine("    float shadowFadeStart;")
			.AppendLine("};");

		// Constant buffers are passed as arguments to entrypoint function in Metal:
		WriteResourceForMetal(in _ctx, "device const CBScene& cbScene [[ buffer( 0 ) ]]");

		_ctx.globalDeclarations.Add(nameConst);
		return success;
	}

	public static bool WriteConstantBuffer_CBCamera(in ShaderGenContext _ctx)
	{
		const string nameConst = "CBCamera";
		if (_ctx.globalDeclarations.Contains(nameConst)) return true;

		bool success = true;

		string typeNameMtx = _ctx.language == ShaderGenLanguage.GLSL
			? "mat4"
			: "float4x4";
		string typeNameVec = _ctx.language == ShaderGenLanguage.GLSL
			? "vec4"
			: "float4";

		// Write structure header:
		_ctx.constants.AppendLine("// Constant buffer containing all settings that apply for everything drawn by currently active camera:");
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.constants, _ctx.language,
			[ "cbuffer CBCamera : register(b1)" ],
			[ "struct CBCamera" ],
			[ "layout (binding = 1) uniform CBCamera" ]);

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

		// Constant buffers are passed as arguments to entrypoint function in Metal:
		WriteResourceForMetal(in _ctx, "device const CBCamera& cbCamera [[ buffer( 1 ) ]]");

		_ctx.globalDeclarations.Add(nameConst);
		return success;
	}

	public static bool WriteConstantBuffer_CBObject(in ShaderGenContext _ctx)
	{
		const string nameConst = "CBObject";
		if (_ctx.globalDeclarations.Contains(nameConst)) return true;

		bool success = true;

		string typeNameMtx = _ctx.language == ShaderGenLanguage.GLSL
			? "mat4"
			: "float4x4";
		string typeNameVec = _ctx.language == ShaderGenLanguage.GLSL
			? "vec3"
			: "float3";

		// Write structure header:
		_ctx.constants.AppendLine("// Constant buffer containing all scene-wide settings:");
		success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.constants, _ctx.language,
			[ "cbuffer CBObject : register(b2)" ],
			[ "struct CBObject" ],
			[ "layout (binding = 2) uniform CBObject" ]);

		// Write body:
		_ctx.constants
			.AppendLine("{")
			.AppendLine($"    {typeNameMtx} mtxLocal2World;")
			.AppendLine($"    {typeNameVec} worldPosition;")
			.AppendLine("    float boundingRadius;")
			.AppendLine("};");

		// Constant buffers are passed as arguments to entrypoint function in Metal:
		WriteResourceForMetal(in _ctx, "device const CBObject& cbObject [[ buffer( 2 ) ]]");

		_ctx.globalDeclarations.Add(nameConst);
		return success;
	}

	#endregion
}
