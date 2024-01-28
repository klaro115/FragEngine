using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.ShaderGen.Features;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public sealed class ShaderGenVariant(MeshVertexDataFlags _vertexDataFlags)
{
	#region Fields

	public bool isEnabled = true;
	public readonly MeshVertexDataFlags vertexDataFlags = _vertexDataFlags;

	public readonly StringBuilder arguments = new(256);
	public readonly StringBuilder header = new(256);
	public readonly StringBuilder code = new(650);

	public readonly HashSet<string> localDeclarations = [];

	public string varNameNormals = DEFAULT_VAR_NAME_NORMALS;
	public string varNameUVs = DEFAULT_VAR_NAME_UVs;

	#endregion
	#region Constants

	public const string DEFAULT_VAR_NAME_NORMALS = "inputBasic.normal";
	public const string DEFAULT_VAR_NAME_UVs = "inputBasic.uv";

	#endregion
	#region Methods

	public void Clear()
	{
		arguments.Clear();
		header.Clear();
		code.Clear();

		localDeclarations.Clear();

		varNameNormals = DEFAULT_VAR_NAME_NORMALS;
		varNameNormals = DEFAULT_VAR_NAME_UVs;
	}

	public bool HasDeclaration(string _name) => !string.IsNullOrEmpty(_name) && localDeclarations.Contains(_name);

	public bool WriteFunction_MainPixel(in ShaderGenContext _ctx, StringBuilder _finalBuilder)
	{
		if (_finalBuilder == null) return false;

		bool success = true;

		bool hasExtendedData = vertexDataFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData);
		bool hasBlendShapes = vertexDataFlags.HasFlag(MeshVertexDataFlags.BlendShapes);
		bool hasBoneAnimation = vertexDataFlags.HasFlag(MeshVertexDataFlags.Animations);

		// Check if the required vertex output structs have been declared:
		success &= _ctx.HasGlobalDeclaration(ShaderGenVertexOutputs.NAME_VERTEX_OUTPUT_BASIC);
		if (hasExtendedData)	success &= _ctx.HasGlobalDeclaration(ShaderGenVertexOutputs.NAME_VERTEX_OUTPUT_BASIC);
		if (hasBlendShapes)		success &= _ctx.HasGlobalDeclaration(ShaderGenVertexOutputs.NAME_VERTEX_OUTPUT_BLEND);
		if (hasBoneAnimation)	success &= _ctx.HasGlobalDeclaration(ShaderGenVertexOutputs.NAME_VERTEX_OUTPUT_ANIM);
		if (!success)
		{
			Logger.Instance?.LogError($"Cannot assemble main pixel shader function with missing vertex outputs for variant '{vertexDataFlags}'!");
			return false;
		}

		// Generate the function's header line:
		success &= WriteHeader_MainPixel(in _ctx, hasExtendedData, hasBlendShapes, hasBoneAnimation);
		_finalBuilder.Append(header);

		// Insert main code body:
		_finalBuilder.Append(code);

		// Add return statement and close function:
		_finalBuilder
			.AppendLine("    // Return final color:")
			.AppendLine("    return albedo;")
			.AppendLine("};")
			.AppendLine();

		return success;
	}

	private bool WriteHeader_MainPixel(
		in ShaderGenContext _ctx,
		bool _hasExtendedData,
		bool _hasBlendShapes,
		bool _hasBoneAnimation)
	{
		// Determine final function name:
		header.Clear();
		header.Append("Main_Pixel");

		// Add vertex flags to name:
		if (_hasExtendedData) header.Append('_').Append(ExtendedVertex.shaderEntryPointSuffix);
		if (_hasBlendShapes) header.Append('_').Append(IndexedWeightedVertex.shaderEntryPointSuffix_Blend);
		if (_hasBoneAnimation) header.Append('_').Append(IndexedWeightedVertex.shaderEntryPointSuffix_Anim);

		string nameFunc = header.ToString();
		if (_ctx.globalDeclarations.Contains(nameFunc))
		{
			Logger.Instance?.LogWarning($"Generated shader code already contains a declaration for pixel shader function '{nameFunc}'!");
			return true;
		}

		// Prefix header with return types:
		switch (_ctx.language)
		{
			case ShaderGenLanguage.HLSL:
				header.Insert(0, "half4 ");
				break;
			case ShaderGenLanguage.Metal:
				header.Insert(0, "half4 fragment");
				break;
			default:
				Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
				return false;
		}

		// List vertex input paramaters:
		switch (_ctx.language)
		{
			case ShaderGenLanguage.HLSL:
				{
					header.Append($"(in {BasicVertex.shaderVertexOuputName} inputBasic");

					if (_hasExtendedData) header.Append($", in {ExtendedVertex.shaderVertexOuputName} inputExt");
					if (_hasBlendShapes) header.Append($", in {IndexedWeightedVertex.shaderVertexOuputName_Blend} inputBlend");
					if (_hasBoneAnimation) header.Append($", in {IndexedWeightedVertex.shaderVertexOuputName_Anim} inputAnim");
				}
				break;
			case ShaderGenLanguage.Metal:
				{
					header.Append($"({BasicVertex.shaderVertexOuputName} inputBasic [[ stage_in ]]");

					if (_hasExtendedData) header.Append($", {ExtendedVertex.shaderVertexOuputName} inputExt");
					if (_hasBlendShapes) header.Append($", {IndexedWeightedVertex.shaderVertexOuputName_Blend} inputBlend");
					if (_hasBoneAnimation) header.Append($", {IndexedWeightedVertex.shaderVertexOuputName_Anim} inputAnim");
				}
				break;
			//...
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
					return false;
				}
		}

		// List any further input paramaters from other features: (mostly needed for Metal)
		if (arguments.Length != 0)
		{
			header.Append(", ").Append(arguments);
		}
		if (_ctx.language == ShaderGenLanguage.Metal && _ctx.resources.Length != 0)
		{
			header.Append(_ctx.resources);
		}

		// Close function header:
		switch (_ctx.language)
		{
			case ShaderGenLanguage.HLSL:
				{
					header.AppendLine(") : SV_Target0").AppendLine("{");
				}
				break;
			case ShaderGenLanguage.Metal:
				{
					header.AppendLine(")").AppendLine("{");
				}
				break;
			default:
				{
					Logger.Instance?.LogError($"Feature is not currently supported for shading language '{_ctx.language}'.");
					return false;
				}
		}

		_ctx.globalDeclarations.Add(nameFunc);
		return true;
	}

	#endregion
}
