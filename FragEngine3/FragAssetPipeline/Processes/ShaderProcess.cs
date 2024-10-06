using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using Veldrid;

namespace FragAssetPipeline.Processes;

internal static class ShaderProcess
{
	#region Types

	public sealed class Details(string _resourceKey, string _relativeFilePath, string _entryPointNameBase, ShaderStages _stage, MeshVertexDataFlags _maxVertexFlafs, bool _bundleSourceCode = true)
	{
		public readonly string resourceKey = _resourceKey;
		public readonly string relativeFilePath = _relativeFilePath;
		public readonly string entryPointNameBase = _entryPointNameBase;
		public readonly ShaderStages stage = _stage;
		public readonly MeshVertexDataFlags maxVertexFlags = _maxVertexFlafs;
		public string descriptionTxt = "At_Nyn0_Ly101p140_V100";
		public bool bundleSourceCode = _bundleSourceCode;
	}

	#endregion
	#region Fields

	private static readonly string[] shaderSourceFileExtensions =
	[
		".fsha",
		".hlsl",
		".spv",
		".metal",
		".glsl",
	];

	#endregion
	#region Methods

	public static bool CompileShaderToFSHA(string _inputDir, string _outputDir, Details _details, in ShaderConfig _shaderConfig, out string _outDataFilePath)
	{
		if (_details is null)
		{
			Program.PrintError("Cannot compile shader to FSHA using null process details!");
			_outDataFilePath = string.Empty;
			return false;
		}

		// Prepare export in FSHA-compliant format:
		FshaExportOptions options = new()
		{
			bundleOnlySourceIfCompilationFails = true,
			shaderStage = _details.stage,
			entryPointBase = _details.entryPointNameBase,
			maxVertexVariantFlags = _details.maxVertexFlags,
			compiledDataTypeFlags = CompiledShaderDataType.ALL,
			supportedFeatures = _shaderConfig,
		};

		return CompileShaderToFSHA(_details.relativeFilePath, _inputDir, _outputDir, options, out _outDataFilePath);
	}

	public static bool CompileShaderToFSHA(string _hlslFileRelativePath, string _inputDir, string _outputDir, FshaExportOptions _exportOptions, out string _outDataFilePath)
	{
		if (string.IsNullOrEmpty(_hlslFileRelativePath))
		{
			Program.PrintError("Cannot compile shader to FSHA using null or blank HLSL file path!");
			_outDataFilePath = string.Empty;
			return false;
		}
		if (_exportOptions is null)
		{
			Program.PrintError("Cannot compile shader to FSHA using null export options!");
			_outDataFilePath = string.Empty;
			return false;
		}

		if (!GetOutputPathFromFileName(_hlslFileRelativePath, _inputDir, _outputDir, out string sourceDataFilePath, out _outDataFilePath))
		{
			_outDataFilePath = string.Empty;
			return false;
		}

		string outputDir = Path.GetDirectoryName(_outDataFilePath)!;
		if (!Directory.Exists(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}

		// Export shader data:
		bool success = FshaExporter.ExportShaderFromHlslFile(sourceDataFilePath, _exportOptions, out ShaderData? shaderData);

		// Write shader data file:
		if (success && shaderData is not null)
		{
			using FileStream stream = new(_outDataFilePath, FileMode.Create);
			using BinaryWriter writer = new(stream);

			success &= shaderData.Write(writer, true);
			stream.Close();
		}

		// Print outcome to console:
		Console.Write($"Shader compilation: '{_hlslFileRelativePath}' => ");
		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
		Console.WriteLine(success ? "SUCCESS" : "FAILURE");
		Console.ForegroundColor = prevColor;
		Console.WriteLine();

		return success;
	}

	/// <summary>
	/// Creates a resource metadata file, which is a JSON-serialized description of a resource data file containing one or more resources.
	/// The metadata file uses the ".fres" file extension, and will share the same name as its associated data file. The engine will auto-detect
	/// and import the ".fres" file into objects of type '<see cref="ResourceFileHandle"/>' and '<see cref="ResourceHandle"/>'.
	/// </summary>
	/// <param name="_inputDir">The asset directory from which shader source files are read.</param>
	/// <param name="_outputDir">The asset directory into which processed shader resource files will be copied.</param>
	/// <param name="_details">Details and compiler instructions for processing this shader resource.</param>
	/// <param name="_outMetadataFilePath">Outputs the file path to the metadata file that was created, located within the output directory.</param>
	/// <returns>True if metadata file creation succeeded, false otherwise.</returns>
	public static bool GenerateResourceMetadataFile(string _inputDir, string _outputDir, Details _details, out string _outMetadataFilePath)
	{
		if (!GetOutputPathFromFileName(_details.relativeFilePath, _inputDir, _outputDir, out _, out string outputPath))
		{
			_outMetadataFilePath = string.Empty;
			return false;
		}
		if (!File.Exists(outputPath))
		{
			Program.PrintError($"Shader resource output file does not exist! File path: '{outputPath}'");
			_outMetadataFilePath = string.Empty;
			return false;
		}

		// Assemble output and relative file paths:
		_outMetadataFilePath = Path.ChangeExtension(outputPath, ".fres");

		string dataFileRelPath = $"./{Path.GetFileName(outputPath)}";

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
			DataFilePath = dataFileRelPath,
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
		return resFileData.SerializeToFile(_outMetadataFilePath);
	}

	private static bool GetOutputPathFromFileName(string _fileName, string _inputDir, string _outputDir, out string _outAbsSourceFilePath, out string _outAbsOutputFilePath)
	{
		_outAbsSourceFilePath = string.Empty;

		if (string.IsNullOrEmpty(_fileName))
		{
			Program.PrintError($"Cannot determine exact source file path from null or blank shader file name!");
			_outAbsOutputFilePath = string.Empty;
			return false;
		}

        // Get absolute path to source file:
		_outAbsSourceFilePath = Path.Combine(_inputDir, _fileName);
		if (!Path.IsPathRooted(_outAbsSourceFilePath))
        {
			_outAbsSourceFilePath = Path.GetFullPath(_outAbsSourceFilePath);
        }

		// If the given source file does not exist, try swapping out extensions to other platform-specific shader file formats:
		bool sourceFileExists = File.Exists(_outAbsSourceFilePath);
		if (!sourceFileExists)
		{
			foreach (string shaderFileExt in shaderSourceFileExtensions)
			{
				_outAbsSourceFilePath = Path.ChangeExtension(_outAbsSourceFilePath, shaderFileExt);
				if (sourceFileExists = File.Exists(_outAbsSourceFilePath))
				{
					break;
				}
			}
			if (!sourceFileExists)
			{
				Program.PrintError($"Cannot determine exact shader source file path! File name: '{_fileName}'");
				_outAbsSourceFilePath = string.Empty;
				_outAbsOutputFilePath = string.Empty;
				return false;
			}
		}

		// Assemble absolute path to output data file in FSHA format:
		string outputFileName = Path.HasExtension(_fileName)
			? Path.ChangeExtension(_fileName, ".fsha")
			: $"{_fileName}.fsha";

		_outAbsOutputFilePath = Path.GetFullPath(Path.Combine(_outputDir, outputFileName));
		return true;
	}

	#endregion
}
