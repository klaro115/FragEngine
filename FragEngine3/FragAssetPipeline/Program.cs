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

ShaderData shaderData = new()
{
	Description = new()
	{
		ShaderStage = testShaderStage,
		Variants = new()
		{
			EntryPointNameBase = testShaderEntryPoint,
			VariantVertexFlags =
			[
				MeshVertexDataFlags.BasicSurfaceData,
				MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData,
			],
		},
		CompiledShaders =
		[
			new ShaderDescriptionData.CompiledShaderData()
			{
				type = CompiledShaderDataType.DXBC,
				byteSize = (uint)result.compiledShader.Length,
			}
		],
	},
	ByteCodeDxbc = result.compiledShader,
	//...
};

string outputPath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.fsha"));
using FileStream stream = new(outputPath, FileMode.Create);
using BinaryWriter writer = new(stream);

shaderData.Write(writer);
stream.Close();

Console.WriteLine("\n#### END ####");
