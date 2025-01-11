using FragAssetPipeline.Common;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using Veldrid;

namespace FragAssetPipeline.Processes;

/// <summary>
/// Utility class for processing shader resources for deployment.
/// </summary>
internal static class ShaderProcess
{
	#region Types

	/// <summary>
	/// Details and additional information on how to process and export a shader resource.
	/// </summary>
	/// <param name="_resourceKey">The resource key through which the resource will be accessed.</param>
	/// <param name="_relativeFilePath">File path to the resource, relative to the input directory containing source assets.</param>
	/// <param name="_entryPointNameBase">Base name of entry point functions within the source code.</param>
	/// <param name="_stage">The shader stage within the rasterized pipeline that this resource's shader programs will be bound to.</param>
	/// <param name="_maxVertexFlags">Maximum vertex flags that should be pre-compiled; variants with other flags may still be compiled from source code at run-time.</param>
	/// <param name="_bundleSourceCode">Whether to include original source code in the exported FSHA file. If false, run-time compilation of additional variants won't be possible.</param>
	public sealed class Details(string _resourceKey, string _relativeFilePath, string _entryPointNameBase, ShaderStages _stage, MeshVertexDataFlags _maxVertexFlags, bool _bundleSourceCode = true)
	{
		public readonly string resourceKey = _resourceKey;
		public readonly string relativeFilePath = _relativeFilePath;
		public readonly string entryPointNameBase = _entryPointNameBase;
		public readonly ShaderStages stage = _stage;
		public readonly MeshVertexDataFlags maxVertexFlags = _maxVertexFlags;
		public string descriptionTxt = "At_Nyn0_Ly101p140_V100";
		public bool bundleSourceCode = _bundleSourceCode;

		public ShaderConfig Config
		{
			get => ShaderConfig.TryParseDescriptionTxt(descriptionTxt, out ShaderConfig config) ? config : default;
			set => descriptionTxt = value.CreateDescriptionTxt();
		}
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

	private static readonly ImporterContext exportCtx = new()
	{
		Logger = new ConsoleLogger(),
		JsonOptions = null,
	};

	#endregion
	#region Methods

	/// <summary>
	/// Pre-compiles and packages a shader asset into FSHA format that can be used directly be the engine.
	/// </summary>
	/// <param name="_inputDir">Input root directory of all shader source files.</param>
	/// <param name="_outputDir">Output root directory for all shader asset files.</param>
	/// <param name="_details">Details for exporting and compiling the shader resource.</param>
	/// <param name="_shaderConfig">The shader configuration specifying features and flags that should be set when compiling shader variants.</param>
	/// <param name="_outDataFilePath">Outputs a file path to the resulting exported data file in FSHA format.</param>
	/// <returns>True if compilation and bundling succeeded, false otherwise.</returns>
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

		return CompileShaderToFSHA(_details.relativeFilePath, _details.resourceKey, _inputDir, _outputDir, options, out _outDataFilePath);
	}

	public static bool CompileShaderToFSHA(string _hlslFileRelativePath, string? _overrideFileName, string _inputDir, string _outputDir, FshaExportOptions _exportOptions, out string _outDataFilePath)
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

		if (!GetOutputPathFromFileName(_hlslFileRelativePath, _overrideFileName, _inputDir, _outputDir, out string sourceDataFilePath, out _outDataFilePath))
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

			success &= FragAssetFormats.Shaders.FSHA.FshaExporter.ExportToFSHA(in exportCtx, writer, shaderData, true);
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
	public static bool GenerateResourceMetadataFile(string _inputDir, string _outputDir, string? _overrideFileName, Details _details, out string _outMetadataFilePath)
	{
		if (!GetOutputPathFromFileName(_details.relativeFilePath, _overrideFileName, _inputDir, _outputDir, out _, out string outputDataFilePath))
		{
			_outMetadataFilePath = string.Empty;
			return false;
		}
		if (!File.Exists(outputDataFilePath))
		{
			Program.PrintError($"Shader resource output file does not exist! File path: '{outputDataFilePath}'");
			_outMetadataFilePath = string.Empty;
			return false;
		}

		// Assemble output and relative file paths: (name metadata file after the resource key)
		string outputDirPath = Path.GetDirectoryName(outputDataFilePath) ?? "./";
		_outMetadataFilePath = Path.Combine(outputDirPath, $"{_details.resourceKey}{ResourceConstants.FILE_EXT_METADATA}");

		string dataFileRelPath = $"./{Path.GetFileName(outputDataFilePath)}";

		// Calculate hash and measure size of output data file:
		if (!ResourceFileHandle.CalculateDataFileHash(outputDataFilePath, out ulong dataFileHash, out ulong dataFileSize))
		{
			Program.PrintError($"Failed to calculate hash and measure file size of shader resource data file! File path: '{outputDataFilePath}'");
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

	private static bool GetOutputPathFromFileName(string _relFilePath, string? _overrideFileName, string _inputDir, string _outputDir, out string _outAbsSourceFilePath, out string _outAbsOutputFilePath)
	{
		_outAbsSourceFilePath = string.Empty;

		if (string.IsNullOrEmpty(_relFilePath))
		{
			Program.PrintError($"Cannot determine exact source file path from null or blank shader file name!");
			_outAbsOutputFilePath = string.Empty;
			return false;
		}

        // Get absolute path to source file:
		_outAbsSourceFilePath = Path.Combine(_inputDir, _relFilePath);
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
				Program.PrintError($"Cannot determine exact shader source file path! File name: '{_relFilePath}'");
				_outAbsSourceFilePath = string.Empty;
				_outAbsOutputFilePath = string.Empty;
				return false;
			}
		}

		// Use a different name for the ouput file, if provided:
		string outputFileName = _relFilePath;
		if (!string.IsNullOrEmpty(_overrideFileName))
		{
			string? relDirPath = Path.GetDirectoryName(_overrideFileName);
			outputFileName = !string.IsNullOrEmpty(relDirPath)
				? Path.Combine(relDirPath, _overrideFileName)
				: _overrideFileName;
		}

		// Assemble absolute path to output data file in FSHA format:
		outputFileName = Path.HasExtension(outputFileName)
			? Path.ChangeExtension(outputFileName, ".fsha")
			: $"{outputFileName}.fsha";

		_outAbsOutputFilePath = Path.GetFullPath(Path.Combine(_outputDir, outputFileName));
		return true;
	}

	#endregion
}
