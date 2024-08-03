using FragAssetPipeline.Resources;
using FragAssetPipeline.Resources.Shaders;
using Veldrid;

Console.WriteLine("### BEGIN ###\n");

string testShaderFilePath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, "shaders/Basic_VS.hlsl"));
const ShaderStages testShaderStage = ShaderStages.Vertex;
const string testShaderEntryPoint = "Main_Vertex";

var result = DxcLauncher.CompileShaderToDXIL(testShaderFilePath, testShaderStage, testShaderEntryPoint);

Console.WriteLine($"Shader compilation: {(result.isSuccess ? "SUCCESS" : "FAILURE")}");

if (result.isSuccess)
{
	Console.WriteLine();
	Console.WriteLine("### DXIL Code: ###");
	Console.WriteLine(result.compiledCode);
}

Console.WriteLine("\n#### END ####");
