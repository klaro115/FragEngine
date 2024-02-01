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
		success &= ShaderGenUtility.WriteLanguageCodeLine(_ctx.constants, _ctx.language,
			"cbuffer CBScene : register(b0)",
			"struct CBScene",
			"layout (binding = 0) uniform CBScene");

		// Write body:
		_ctx.constants.AppendLine(
			"{\n" +
			"    // Scene lighting:")
			.Append("    ").Append(typeNameVec).AppendLine(" ambientLightLow;")
			.Append("    ").Append(typeNameVec).AppendLine(" ambientLightMid;")
			.Append("    ").Append(typeNameVec).AppendLine(" ambientLightHigh;")
			.AppendLine(
			"    float shadowFadeStart;\n" +
			"};");

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
		success &= ShaderGenUtility.WriteLanguageCodeLine(_ctx.constants, _ctx.language,
			"cbuffer CBCamera : register(b1)",
			"struct CBCamera",
			"layout (binding = 1) uniform CBCamera");

		// Write body:
		_ctx.constants.AppendLine(
			"{\n" +
			"    // Camera vectors & matrices:")
			.Append("    ").Append(typeNameMtx).AppendLine(" mtxWorld2Clip;")
			.Append("    ").Append(typeNameVec).AppendLine(" cameraPosition;")
			.Append("    ").Append(typeNameVec).AppendLine(" cameraDirection;")
			.Append("    ").Append(typeNameMtx).AppendLine(" mtxCameraMotion;")
			.AppendLine(
			"\n" +
			"    // Camera parameters:\n" +
			"    uint cameraIdx;\n" +
			"    uint resolutionX;\n" +
			"    uint resolutionY;\n" +
			"    float nearClipPlane;\n" +
			"    float farClipPlane;\n" +
			"\n" +
			"    // Per-camera lighting:\n" +
			"    uint lightCount;\n" +
			"    uint shadowMappedLightCount;\n" +
			"};");

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
		success &= ShaderGenUtility.WriteLanguageCodeLine(_ctx.constants, _ctx.language,
			"cbuffer CBObject : register(b2)",
			"struct CBObject",
			"layout (binding = 2) uniform CBObject");

		// Write body:
		_ctx.constants.AppendLine(
			"{")
			.Append("    ").Append(typeNameMtx).AppendLine(" mtxLocal2World;")
			.Append("    ").Append(typeNameVec).AppendLine(" worldPosition;")
			.AppendLine(
			"    float boundingRadius;\n" +
			"};");

		// Constant buffers are passed as arguments to entrypoint function in Metal:
		WriteResourceForMetal(in _ctx, "device const CBObject& cbObject [[ buffer( 2 ) ]]");

		_ctx.globalDeclarations.Add(nameConst);
		return success;
	}

	#endregion
}
