using FragAssetPipeline.Resources;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using Veldrid;

Console.WriteLine("### BEGIN ###\n");

//const string testShaderName = "DefaultSurface_modular_PS";
//const string testShaderEntryPoint = "Main_Pixel";
//const ShaderStages testShaderStage = ShaderStages.Fragment;
const string testShaderName = "DefaultSurface_VS";
const string testShaderEntryPoint = "Main_Vertex";
const ShaderStages testShaderStage = ShaderStages.Vertex;

string testShaderFilePath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.hlsl"));

// Export serializable shader data in FSHA-compliant format:
FshaExportOptions exportOptions = new()
{
	bundleOnlySourceIfCompilationFails = true,
	shaderStage = testShaderStage,
	entryPointBase = testShaderEntryPoint,
	maxVertexVariantFlags = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData,
	compiledDataTypeFlags = CompiledShaderDataType.ALL,
};

bool success = FshaExporter.ExportShaderFromHlslFile(testShaderFilePath, exportOptions, out ShaderData? shaderData);

Console.WriteLine($"Shader compilation: {(success ? "SUCCESS" : "FAILURE")}");

string outputPath = Path.GetFullPath(Path.Combine(ResourceConstants.coreFolderRelativePath, $"shaders/{testShaderName}.fsha"));

//TEST TEST TEST TEST
byte[]? spirvCode = shaderData!.GetByteCodeOfType(CompiledShaderDataType.SPIRV);
if (spirvCode is not null && shaderData.Description.GetCompiledShaderVariantData(CompiledShaderDataType.SPIRV, MeshVertexDataFlags.BasicSurfaceData, null, out var spirvVariantData))
{
	byte[] spirvVariantBytes = new byte[spirvVariantData.ByteSize];
	Array.Copy(spirvCode, spirvVariantData.ByteOffset, spirvVariantBytes, 0, spirvVariantData.ByteSize);
	string spirvOutputPath = Path.ChangeExtension(outputPath, ".spv");
	File.WriteAllBytes(spirvOutputPath, spirvVariantBytes);
}
//TEST TEST TEST TEST

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
