using FragAssetPipeline.Resources;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using Veldrid;

namespace FragAssetPipeline;

internal sealed class ShaderDetails(string _fileName, string _entryPointNameBase, ShaderStages _stage, MeshVertexDataFlags _maxVertexFlafs)
{
	public readonly string fileName = _fileName;
	public readonly string entryPointNameBase = _entryPointNameBase;
	public readonly ShaderStages stage = _stage;
	public readonly MeshVertexDataFlags maxVertexFlags = _maxVertexFlafs;
	public string descriptionTxt = "At_Nyn0_Ly101p140_V100";
}

internal static class ShaderProcess
{
	#region Methods

	public static void CompileShaderToFSHA(string _hlslFileName, FshaExportOptions _exportOptions)
	{
		string shadersDirRelativePath = Path.Combine(ResourceConstants.coreFolderRelativePath, "shaders/");
		string testShaderFilePath = Path.GetFullPath(Path.Combine(shadersDirRelativePath, $"{_hlslFileName}.hlsl"));
		string outputPath = Path.GetFullPath(Path.Combine(shadersDirRelativePath, $"{_hlslFileName}.fsha"));

		bool success = FshaExporter.ExportShaderFromHlslFile(testShaderFilePath, _exportOptions, out ShaderData? shaderData);

		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
		Console.WriteLine($"Shader compilation: '{_hlslFileName}' => {(success ? "SUCCESS" : "FAILURE")}\n");
		Console.ForegroundColor = prevColor;

		// Write shader file:
		if (success && shaderData is not null)
		{
			using FileStream stream = new(outputPath, FileMode.Create);
			using BinaryWriter writer = new(stream);

			success &= shaderData.Write(writer, true);
			stream.Close();
		}
	}

	#endregion
}
