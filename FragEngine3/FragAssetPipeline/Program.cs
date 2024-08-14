using FragAssetPipeline.Resources;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using Veldrid;

Console.WriteLine("### BEGIN ###\n");

const string testShaderName = "DefaultSurface_modular_PS";
string testShaderFilePath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.hlsl"));
const ShaderStages testShaderStage = ShaderStages.Fragment;
const string testShaderEntryPoint = "Main_Pixel";

// Check FSHA exporter's platform support:
bool success = FshaExporter.IsAvailableOnCurrentPlatform();
if (!success)
{
	Console.WriteLine("Shader compilation: Not supported on this platform.");
}

// Export serializable shader data in FSHA-compliant format:
ShaderData? shaderData = null;
if (success)
{
	success &= FshaExporter.ExportShaderFromHlslFile(testShaderFilePath, testShaderEntryPoint, testShaderStage, out shaderData);

	Console.WriteLine($"Shader compilation: {(success ? "SUCCESS" : "FAILURE")}");
}

string outputPath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.fsha"));

// Write shader file:
if (success && shaderData is not null)
{
	using FileStream stream = new(outputPath, FileMode.Create);
	using BinaryWriter writer = new(stream);

	success &= shaderData.Write(writer, true);
	stream.Close();
}

// Read shader file:
if (success)
{
	using FileStream stream = new(outputPath, FileMode.Open, FileAccess.Read);
	using BinaryReader reader = new(stream);

	success &= ShaderData.Read(reader, out ShaderData? shaderData2, CompiledShaderDataType.ALL);
	stream.Close();
}

Console.WriteLine("\n#### END ####");
