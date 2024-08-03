using FragAssetPipeline.Resources;
using FragAssetPipeline.Resources.Shaders;
using Veldrid;
using Vortice.Dxc;

Console.WriteLine("### BEGIN ###\n");

string testShaderFilePath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, "shaders/Basic_VS.hlsl"));
//const ShaderStages testShaderStage = ShaderStages.Vertex;
const string testShaderEntryPoint = "Main_Vertex";

/*
var result = DxcLauncher.CompileShaderToDXIL(testShaderFilePath, testShaderStage, testShaderEntryPoint);

Console.WriteLine($"Shader compilation: {(result.isSuccess ? "SUCCESS" : "FAILURE")}");

if (result.isSuccess)
{
	Console.WriteLine();
	Console.WriteLine("### DXIL Code: ###");
	Console.WriteLine(result.compiledCode);
}
*/
string hlslCode = File.ReadAllText(testShaderFilePath);

DxcCompilerOptions options = new()
{
	ShaderModel = DxcShaderModel.Model6_7,
	
	// SPIR-V:
	GenerateSpirv = false,
	VkUseDXLayout = true,
	VkUseDXPositionW = true,
};

using var results = DxcCompiler.Compile(DxcShaderStage.Vertex, hlslCode, testShaderEntryPoint, options);
for (int i = 0; i < results.NumOutputs; i++)
{
	var kind = results.GetOutputByIndex(i);
	switch (kind)
	{
		case DxcOutKind.Object:
			{
				using var blob = results.GetOutput(kind);
				if (blob.BufferSize > 0)
				{
					Console.WriteLine("Success.");

					byte[] bytes = blob.AsBytes();
					string output = System.Text.Encoding.UTF8.GetString(bytes);
					Console.WriteLine(output);
				}
			}
			break;
		case DxcOutKind.Errors:
			{
				using var blob = results.GetOutput(kind);
				if (blob.BufferSize != 0)
				{
					Console.WriteLine("Error.");
				}
			}
			break;
		default:
			break;
	}
}

Console.WriteLine("\n#### END ####");
