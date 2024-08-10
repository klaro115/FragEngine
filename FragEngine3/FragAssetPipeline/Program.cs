using FragAssetPipeline.Resources;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using Veldrid;

Console.WriteLine("### BEGIN ###\n");

const string testShaderName = "Basic_VS";
string testShaderFilePath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.hlsl"));
const ShaderStages testShaderStage = ShaderStages.Vertex;
const string testShaderEntryPoint = "Main_Vertex";

var result = DxcLauncher.CompileShaderToDXBC(testShaderFilePath, testShaderStage, testShaderEntryPoint);

Console.WriteLine($"Shader compilation: {(result.isSuccess ? "SUCCESS" : "FAILURE")}");

byte[] testShaderSourceCodeUtf8 = File.ReadAllBytes(testShaderFilePath);

ShaderData shaderData = new()
{
	Description = new()
	{
		ShaderStage = testShaderStage,
		SourceCode = new ShaderDescriptionSourceCodeData()
		{
			EntryPointNameBase = testShaderEntryPoint,
			EntryPoints =
			[
				new()
				{
					EntryPoint = testShaderEntryPoint,
					VariantFlags = MeshVertexDataFlags.BasicSurfaceData,
				},
				new()
				{
					EntryPoint = testShaderEntryPoint + "_Ext",
					VariantFlags = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData,
				},
			],
			SupportedFeaturesTxt = "Ac_Nnn_Ln000p0_V100",
			MaximumCompiledFeaturesTxt = "Ac_Nnn_Ln000p0_V100"
		},
		CompiledVariants =
		[
			new ShaderDescriptionVariantData()
			{
				Type = CompiledShaderDataType.DXBC,
				VariantFlags = MeshVertexDataFlags.BasicSurfaceData,
				VariantDescriptionTxt = "Ac_Nnn_Ln000p0_V000",
				EntryPoint = testShaderEntryPoint,
				ByteOffset = 0,
				ByteSize = (uint)result.compiledShader.Length,
			},
		],
	},
	SourceCode = testShaderSourceCodeUtf8,
	ByteCodeDxbc = result.compiledShader,
	//...
};

string outputPath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.fsha"));

// Write shader file:
using (FileStream stream = new(outputPath, FileMode.Create))
{
	using BinaryWriter writer = new(stream);

	shaderData.Write(writer, true);
	stream.Close();
}

// Read shader file:
using (FileStream stream = new(outputPath, FileMode.Open, FileAccess.Read))
{
	using BinaryReader reader = new(stream);

	ShaderData.Read(reader, out ShaderData? shaderData2, CompiledShaderDataType.ALL);
	stream.Close();
}

Console.WriteLine("\n#### END ####");
