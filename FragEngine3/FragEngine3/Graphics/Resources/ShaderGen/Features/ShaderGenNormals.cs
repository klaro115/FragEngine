using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.ShaderGen.Features;

public static class ShaderGenNormals
{
	#region Methods

	public static bool WriteResource_NormalMap(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		bool success = true;

		// Declare main texture "TexNormal":
		const string nameTexNormal = "TexNormal";
		bool alreadyDeclaredTexNormal = _ctx.globalDeclarations.Contains(nameTexNormal);

		if (!alreadyDeclaredTexNormal)
		{
			success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.resources, _ctx.language,
				[ $"Texture2D<half4> TexNormal : register(ps, t{_ctx.boundTextureIdx});" ],
				[ $", texture2d<half, access::sample> TexNormal [[ texture( {_ctx.boundTextureIdx} ) ]]" ],
				null,
				_ctx.language != ShaderGenLanguage.Metal);

			_ctx.boundTextureIdx++;
			_ctx.globalDeclarations.Add(nameTexNormal);
		}

		// Declare custom normal map sampler or re-use main texture's sampler:
		string nameSamplerNormal = !string.IsNullOrEmpty(_config.samplerTexNormal)
			? _config.samplerTexNormal
			: "SamplerMain";
		bool alreadyDeclaredSamplerNormal = _ctx.globalDeclarations.Contains(nameSamplerNormal);

		if (!alreadyDeclaredSamplerNormal)
		{
			success &= ShaderGenUtility.WriteLanguageCodeLines(_ctx.resources, _ctx.language,
				[ $"SamplerState {nameSamplerNormal} : register(s{_ctx.boundSamplerIdx});" ],
				[ $", sampler {nameSamplerNormal} [[ ??? ]]" ], //TEMP
				null,
				_ctx.language != ShaderGenLanguage.Metal);

			_ctx.boundSamplerIdx++;
			_ctx.globalDeclarations.Add(nameSamplerNormal);
		}

		return success;
	}

	private static bool WriteFunction_UnpackNormals(in ShaderGenContext _ctx)
	{
		const string nameFunc = "UnpackNormalMap";
		if (_ctx.HasGlobalDeclaration(nameFunc)) return true;

		bool success = true;

		// Write function header:
		ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			[ "half3 UnpackNormalMap(in half3 _texNormal)" ],
			[ "half3 UnpackNormalMap(const half3& _texNormal)" ],
			null);

		// Write function body:
		_ctx.functions
			.AppendLine("{")
			.AppendLine("    // Unpack direction vector from normal map colors:")
			.AppendLine("    return half3(_texNormal.x * 2 - 1, _texNormal.z, _texNormal.y * 2 - 1);")
			.AppendLine("}")
			.AppendLine();

		_ctx.globalDeclarations.Add(nameFunc);
		return success;
	}

	private static bool WriteFunction_ApplyNormalMap(in ShaderGenContext _ctx)
	{
		const string nameFunc = "ApplyNormalMap";
		if (_ctx.HasGlobalDeclaration(nameFunc)) return true;

		bool success = true;

		success &= WriteFunction_UnpackNormals(in _ctx);

		// Write function header:
		ShaderGenUtility.WriteLanguageCodeLines(_ctx.functions, _ctx.language,
			[ "half3 ApplyNormalMap(in half3 _worldNormal, in half3 _worldTangent, in half3 _worldBinormal, in half3 _texNormal)" ],
			[ "half3 ApplyNormalMap(const half3& _worldNormal, const half3& _worldTangent, const half3& _worldBinormal, half3& _texNormal)" ],
			null);

		// Write function body:
		_ctx.functions
			.AppendLine("{")
			.AppendLine("    _texNormal = UnpackNormalMap(_texNormal);")
			.AppendLine()
			.AppendLine("    // Create rotation matrix, projecting from flat surface (UV) space to surface in world space:")
			.AppendLine("    const half3x3 mtxNormalRot =")
			.AppendLine("    {")
			.AppendLine("        _worldBinormal.x, _worldNormal.x, _worldTangent.x,")
			.AppendLine("        _worldBinormal.y, _worldNormal.y, _worldTangent.y,")
			.AppendLine("        _worldBinormal.z, _worldNormal.z, _worldTangent.z,")
			.AppendLine("    };")
			.AppendLine("    half3 normal = mul(mtxNormalRot, _texNormal);")
			.AppendLine("    return normal;")
			.AppendLine("}")
			.AppendLine();

		_ctx.globalDeclarations.Add(nameFunc);
		return success;
	}

	public static bool WriteVariable_NormalMap(in ShaderGenContext _ctx, in ShaderGenConfig _config)
	{
		const string nameVar = "normal";

		string nameSamplerNormal = !string.IsNullOrEmpty(_config.samplerTexNormal)
			? _config.samplerTexNormal
			: "SamplerMain";

		bool success = true;

		// Ensure the resources (texture & sampler) for normal maps are declared:
		success &= WriteResource_NormalMap(in _ctx, in _config);

		// Ensure the normal processing function is declared:
		success &= WriteFunction_ApplyNormalMap(in _ctx);

		foreach (ShaderGenVariant variant in _ctx.variants)
		{
			bool alreadyDeclared = variant.HasDeclaration(nameVar);

			string nameVarInputNormal = ShaderGenUtility.SelectName(variant.varNameNormals, ShaderGenVariant.DEFAULT_VAR_NAME_NORMALS);
			string nameVarUVs = ShaderGenUtility.SelectName(variant.varNameUVs, ShaderGenVariant.DEFAULT_VAR_NAME_UVs);

			bool hasExtendedData = variant.vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
			string nameVarTangent = hasExtendedData ? "(half3)inputExt.tangent" : "half3(0, 0, 1)";
			string nameVarBinormal = hasExtendedData ? "(half3)inputExt.binormal" : "half3(1, 0, 0)";

			// Declare variable or overwrite its value:
			variant.code.Append("    ");
			if (!alreadyDeclared)
			{
				variant.code.Append("half3 ");
			}
			variant.code.Append(nameVar).Append(" = ");

			// Sample from normal map texture:
			switch (_ctx.language)
			{
				case ShaderGenLanguage.HLSL:
					{
						// Sample normal map:
						variant.code
							.Append("TexNormal.Sample(")
							.Append(nameSamplerNormal)
							.Append(", ")
							.Append(nameVarUVs)
							.AppendLine(");");

						// Transform normal map output into the surface's normal space:
						variant.code
							.Append("    ")
							.Append(nameVar)
							.Append(" = ApplyNormalMap(")
							.Append(nameVarInputNormal)
							.Append(", ")
							.Append(nameVarTangent)
							.Append(", ")
							.Append(nameVarBinormal)
							.Append(", ")
							.Append(nameVar)
							.AppendLine(");");
					}
					break;
				case ShaderGenLanguage.Metal:
					{
						// Sample normal map:
						variant.code
							.Append("TexNormal.sample(")
							.Append(nameSamplerNormal)
							.Append(", ")
							.Append(nameVarUVs)
							.AppendLine(");");

						// Transform normal map output into the surface's normal space:
						variant.code
							.Append("    ")
							.Append(nameVar)
							.Append(" = ApplyNormalMap(")
							.Append(nameVarInputNormal)
							.Append(", ")
							.Append(nameVarTangent)
							.Append(", ")
							.Append(nameVarBinormal)
							.Append(", ")
							.Append(nameVar)
							.AppendLine(");");
					}
					break;
				default:
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
					return false;
			}

			variant.code.AppendLine();

			// Remap all further uses of surface normals to use the new variable:
			variant.varNameNormals = nameVar;
			if (!alreadyDeclared) variant.localDeclarations.Add(nameVar);
		}

		return success;
	}

	#endregion
}
