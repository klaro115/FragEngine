using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using Veldrid;

namespace FragAssetPipeline.Processes;

internal sealed class ShaderProcessDetails(string _resourceKey, string _fileName, string _entryPointNameBase, ShaderStages _stage, MeshVertexDataFlags _maxVertexFlafs)
{
	public readonly string resourceKey = _resourceKey;
	public readonly string fileName = _fileName;
	public readonly string entryPointNameBase = _entryPointNameBase;
	public readonly ShaderStages stage = _stage;
	public readonly MeshVertexDataFlags maxVertexFlags = _maxVertexFlafs;
	public string descriptionTxt = "At_Nyn0_Ly101p140_V100";
}

internal static class ShaderProcess
{
	#region Fields

	private static readonly string[] shaderFileExtensions =
	[
		".fsha",
		".hlsl",
		".spv",
		".metal",
		".glsl",
	];

	#endregion
	#region Methods

	public static bool CompileShaderToFSHA(ShaderProcessDetails _details, in ShaderConfig _shaderConfig)
	{
		if (_details is null)
		{
			Program.PrintError("Cannot compile shader to FSHA using null process details!");
			return false;
		}

		// Export serializable shader data in FSHA-compliant format:
		FshaExportOptions options = new()
		{
			bundleOnlySourceIfCompilationFails = true,
			shaderStage = _details.stage,
			entryPointBase = _details.entryPointNameBase,
			maxVertexVariantFlags = _details.maxVertexFlags,
			compiledDataTypeFlags = CompiledShaderDataType.ALL,
			supportedFeatures = _shaderConfig,
		};

		return CompileShaderToFSHA(_details.fileName, options);
	}

	public static bool CompileShaderToFSHA(string _hlslFileName, FshaExportOptions _exportOptions)
	{
		if (string.IsNullOrEmpty(_hlslFileName))
		{
			Program.PrintError("Cannot compile shader to FSHA using null or blank HLSL file path!");
			return false;
		}
		if (_exportOptions is null)
		{
			Program.PrintError("Cannot compile shader to FSHA using null export options!");
			return false;
		}

		if (!GetOutputPathFromFileName(_hlslFileName, out string shaderFilePath, out string outputPath))
		{
			return false;
		}

		// Export shader:
		bool success = FshaExporter.ExportShaderFromHlslFile(shaderFilePath, _exportOptions, out ShaderData? shaderData);

		// Write shader file:
		if (success && shaderData is not null)
		{
			using FileStream stream = new(outputPath, FileMode.Create);
			using BinaryWriter writer = new(stream);

			success &= shaderData.Write(writer, true);
			stream.Close();
		}

		// Print outcome to console:
		Console.Write($"Shader compilation: '{_hlslFileName}' => ");
		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
		Console.WriteLine(success ? "SUCCESS" : "FAILURE");
		Console.ForegroundColor = prevColor;
		Console.WriteLine();

		return success;
	}

	public static bool GenerateResourceMetadataFile(ShaderProcessDetails _details)
	{
		if (!GetOutputPathFromFileName(_details.fileName, out _, out string outputPath))
		{
			return false;
		}
		if (!File.Exists(outputPath))
		{
			Program.PrintError($"Shader resource output file does not exist! File path: '{outputPath}'");
			return false;
		}

		// Assemble output and relative file paths:
		string outputDirPath = Path.GetDirectoryName(outputPath) ?? "./";
		string metadataFilePath = Path.Combine(outputDirPath, $"{_details.resourceKey}.fres");

		string dataFileName = Path.GetFileName(_details.fileName);
		if (string.IsNullOrEmpty(dataFileName))
		{
			dataFileName = _details.fileName;
		}
		string relativeDataFilePath = $"./{dataFileName}";

		// Calculate hash and measure size of output data file:
		if (!ResourceFileHandle.CalculateDataFileHash(outputPath, out ulong dataFileHash, out ulong dataFileSize))
		{
			Program.PrintError($"Failed to calculate hash and measure file size of shader resource data file! File path: '{outputPath}'");
			return false;
		}
		
		// Assemble serializable data:
		ResourceHandleData resHandleData = new()
		{
			ResourceKey = _details.resourceKey,
			ResourceType = ResourceType.Shader,
			PlatformFlags = 0,
			ImportFlags = null,

			DataOffset = 0,
			DataSize = dataFileSize,

			DependencyCount = 0,
			Dependencies = null,
		};

		ResourceFileData resFileData = new()
		{
			DataFilePath = relativeDataFilePath,
			DataFileType = ResourceFileType.Single,
			DataFileSize = dataFileSize,
			DataFileHash = dataFileHash,

			UncompressedFileSize = dataFileSize,
			BlockSize = 0,
			BlockCount = 0,

			ResourceCount = 1,
			Resources = [ resHandleData ],
		};

		// Write metadata JSON to file:
		return resFileData.SerializeToFile(metadataFilePath);
	}

	private static bool GetOutputPathFromFileName(string _fileName, out string _outSourceFilePath, out string _outOutputFilePath)
	{
		_outSourceFilePath = string.Empty;

		if (string.IsNullOrEmpty(_fileName))
		{
			Program.PrintError($"Cannot determine exact source file path from null or blank shader file name!");
			_outOutputFilePath = string.Empty;
			return false;
		}

		string shadersDirRelativePath = Path.Combine(Resources.ResourceConstants.coreFolderRelativePath, "shaders/");

		bool sourceFileExists = false;
		foreach (string shaderFileExt in shaderFileExtensions)
		{
			_outSourceFilePath = Path.GetFullPath(Path.Combine(shadersDirRelativePath, $"{_fileName}.hlsl"));
			if (sourceFileExists = File.Exists(_outSourceFilePath))
			{
				break;
			}
		}
		if (!sourceFileExists)
		{
			Program.PrintError($"Cannot determine exact shader source file path! File name: '{_fileName}'");
			_outSourceFilePath = string.Empty;
			_outOutputFilePath = string.Empty;
			return false;
		}

		_outOutputFilePath = Path.GetFullPath(Path.Combine(shadersDirRelativePath, $"{_fileName}.fsha"));
		return true;
	}

	#endregion
}
