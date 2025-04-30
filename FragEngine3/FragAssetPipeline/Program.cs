using FragAssetPipeline.Processes;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Shaders;
using Veldrid;

namespace FragAssetPipeline;

internal static class Program
{
	#region Constants

	private static readonly bool clearBuildDirFirst = true;
	private static readonly bool autoGenerateFresFiles = true;

	private const MeshVertexDataFlags flagsBasic = MeshVertexDataFlags.BasicSurfaceData;
	private const MeshVertexDataFlags flagsExt = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	#endregion
	#region Fields

	private static readonly ShaderConfig shaderConfig = new()
	{
		albedoSource = ShaderAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		useNormalMap = true,
		useParallaxMap = false,
		useParallaxMapFull = false,
		applyLighting = true,
		useAmbientLight = true,
		useLightSources = true,
		lightingModel = ShaderLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 4,
		alwaysCreateExtendedVariant = true,
	};
	private static readonly string shaderDescriptionTxt = shaderConfig.CreateDescriptionTxt();

	private static readonly ShaderConfig shaderConfigTexLit = new()
	{
		albedoSource = ShaderAlbedoSource.SampleTexMain,
		albedoColor = RgbaFloat.White,
		useNormalMap = false,
		useParallaxMap = false,
		useParallaxMapFull = false,
		applyLighting = true,
		useAmbientLight = true,
		useLightSources = true,
		lightingModel = ShaderLightingModel.Phong,
		useShadowMaps = true,
		shadowSamplingCount = 8,
		alwaysCreateExtendedVariant = true,
	};
	private static readonly string shaderDescriptionTxtTexList = shaderConfigTexLit.CreateDescriptionTxt();

	private static readonly ShaderProcess.Details[] details =
	[
		new("Basic_VS",                           "Basic_VS.hlsl",                                       "Main_Vertex", ShaderStages.Vertex,   flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("DefaultSurface_VS",                  "DefaultSurface_VS.hlsl",                              "Main_Vertex", ShaderStages.Vertex,   flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("DefaultSurface_PS",                  "DefaultSurface_modular_PS.hlsl",                      "Main_Pixel",  ShaderStages.Fragment, flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("TexturedLit_PS",                     "DefaultSurface_modular_PS.hlsl",                      "Main_Pixel",  ShaderStages.Fragment, flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxtTexList },
		new("Heightmap_VS",                       "Heightmap_VS.hlsl",                                   "Main_Vertex", ShaderStages.Vertex,   flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("AlphaShadow_PS",                     "shadows/AlphaShadow_PS.hlsl",                         "Main_Pixel",  ShaderStages.Fragment, flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("DefaultShadow_PS",                   "shadows/DefaultShadow_PS.hlsl",                       "Main_Pixel",  ShaderStages.Fragment, flagsExt,   _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("ForwardPlusLight_CompositeScene_PS", "composition/ForwardPlusLight_CompositeScene_PS.hlsl", "Main_Pixel",  ShaderStages.Fragment, flagsBasic, _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
		new("ForwardPlusLight_CompositeUI_PS",    "composition/ForwardPlusLight_CompositeUI_PS.hlsl",    "Main_Pixel",  ShaderStages.Fragment, flagsBasic, _bundlePrecompiledData: false) { descriptionTxt = shaderDescriptionTxt },
	];

	private static readonly string[] preprocessedModelNames =
	[
		"RoadDescending.fbx"
	];

	#endregion
	#region Methods

	private static void Main(string[] _)
	{
		Console.WriteLine("### BEGIN ###");

		string? assetPipelineEntryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
		string assetPipelineBuildDir = Path.GetFullPath(assetPipelineEntryPath ?? Environment.CurrentDirectory);

		string inputAssetsAbsDir = Path.GetFullPath(Path.Combine(assetPipelineBuildDir, ProgramConstants.inputAssetsRelativePath));		// "Assets" folder
		string outputAssetsAbsDir = Path.GetFullPath(Path.Combine(assetPipelineBuildDir, ProgramConstants.outputAssetsRelativePath));	// "data" folder
		string buildAssetsAbsDir = Path.GetFullPath(Path.Combine(assetPipelineBuildDir, ProgramConstants.buildAssetsRelativePath));		// "data" folder in TestApp's build directory

		if (!Directory.Exists(inputAssetsAbsDir))
		{
			PrintError($"Asset input directory does not exist! Path: '{inputAssetsAbsDir}'");
			Console.WriteLine("\n#### END ####");
			return;
		}
		if (!Directory.Exists(outputAssetsAbsDir))
		{
			Directory.CreateDirectory(outputAssetsAbsDir);
		}
		if (!Directory.Exists(buildAssetsAbsDir))
		{
			Directory.CreateDirectory(buildAssetsAbsDir);
		}

		if (clearBuildDirFirst)
		{
			PrintStatus("\n## CLEARING DESTINATIONS:");
			ClearDestinationFolders(outputAssetsAbsDir, buildAssetsAbsDir);
		}

		List<string> resourceFilePaths = [];

		PrintStatus("\n## PROCESSING SHADERS:");
		ProcessShaders(inputAssetsAbsDir, outputAssetsAbsDir, resourceFilePaths);

		PrintStatus("\n## PROCESSING MODELS:");
		ProcessModels(inputAssetsAbsDir, outputAssetsAbsDir, resourceFilePaths);
		//ProcessGenericResources(inputAssetsAbsDir, outputAssetsAbsDir, "models", resourceFilePaths);

		PrintStatus("\n## PROCESSING TEXTURES:");
		ProcessGenericResources(inputAssetsAbsDir, outputAssetsAbsDir, "textures", resourceFilePaths);

		PrintStatus("\n## PROCESSING MATERIALS:");
		ProcessGenericResources(inputAssetsAbsDir, outputAssetsAbsDir, "materials", resourceFilePaths);

		//...

		PrintStatus("\n## BUNDLING RESOURCES:");
		ProcessBundling(resourceFilePaths, outputAssetsAbsDir);

		PrintStatus("\n## PROCESSING OUTPUT:");
		ProcessOutput(resourceFilePaths, outputAssetsAbsDir, buildAssetsAbsDir);

		Console.WriteLine("\n#### END ####");
	}

	private static bool ClearDestinationFolders(string _outputAssetsAbsDir, string _buildAssetsAbsDir)
	{
		bool success = true;

		Console.WriteLine($"Clearing data folder...");
		if (!ClearingProcess.ClearFolder(_outputAssetsAbsDir, true))
		{
			PrintError("Failed to clear data folder!");
			success = false;
		}

		Console.WriteLine($"Clearing build folder...");
		if (!ClearingProcess.ClearFolder(_buildAssetsAbsDir, true))
		{
			PrintError("Failed to clear resources in build directory!");
			success = false;
		}

		return success;
	}

	private static bool ProcessShaders(string _inputAssetDir, string _outputAssetDir, List<string> _dstResourceFilePaths)
	{
		string inputShaderDir = Path.Combine(_inputAssetDir, "shaders");
		string outputShaderDir = Path.Combine(_outputAssetDir, "shaders");

		// Ensure input and output directories exist; create output if missing:
		if (!Directory.Exists(inputShaderDir))
		{
			PrintError($"Input directory for shader process does not exist! Path: '{inputShaderDir}'");
			return false;
		}
		if (!Directory.Exists(outputShaderDir))
		{
			Directory.CreateDirectory(outputShaderDir);
		}

		// Process and output shaders one after the other:
		int successCount = 0;
		int totalShaderCount = details.Length;

		foreach (ShaderProcess.Details detail in details)
		{
			// Compile and bundle shader data file in FSHA format:
			if (!ShaderProcess.CompileShaderToFSHA(inputShaderDir, outputShaderDir, detail, detail.Config, out string dataFilePath))
			{
				continue;
			}
			// Optionally, generate a metadata file to go with the data file:
			if (autoGenerateFresFiles)
			{
				if (!ShaderProcess.GenerateResourceMetadataFile(inputShaderDir, outputShaderDir, detail.resourceKey, detail, out string metadataFilePath))
				{
					continue;
				}
				_dstResourceFilePaths.Add(metadataFilePath);
			}
			else
			{
				_dstResourceFilePaths.Add(dataFilePath);
			}
			successCount++;
		}

		// Print a brief summary of processing results:
		if (successCount < totalShaderCount)
		{
			PrintWarning($"Processing of {totalShaderCount - successCount}/{totalShaderCount} shader resources failed!");
		}
		else
		{
			Console.WriteLine($"Processing of all {totalShaderCount} shader resources succeeded.");
		}
		return successCount == totalShaderCount;
	}

	private static bool ProcessModels(string _inputAssetDir, string _outputAssetDir, List<string> _dstResourceFilePaths)
	{
		string inputModelDir = Path.Combine(_inputAssetDir, "models");
		string outputModelDir = Path.Combine(_outputAssetDir, "models");

		// Ensure input and output directories exist; create output if missing:
		if (!Directory.Exists(inputModelDir))
		{
			PrintError($"Input directory for model process does not exist! Path: '{inputModelDir}'");
			return false;
		}
		if (!Directory.Exists(outputModelDir))
		{
			Directory.CreateDirectory(outputModelDir);
		}

		//TODO 1: Add 3D model process.
		//TODO 2: Treat model files in `preprocessedModelNames` seperately => convert them, copy all others.

		return GenericResourceProcess.PrepareResources(inputModelDir, outputModelDir, _dstResourceFilePaths);
	}

	private static bool ProcessGenericResources(string _inputAssetDir, string _outputAssetDir, string _assetCategoryDirName, List<string> _dstResourceFilePaths)
	{
		string inputSubDir = Path.Combine(_inputAssetDir, _assetCategoryDirName);
		string outputSubDir = Path.Combine(_outputAssetDir, _assetCategoryDirName);

		return GenericResourceProcess.PrepareResources(inputSubDir, outputSubDir, _dstResourceFilePaths);
	}

	private static bool ProcessBundling(List<string> _srcResourceMetadataPaths, string _dstAssetsAbsDir)
	{
		if (_srcResourceMetadataPaths.Count	< 2)
		{
			PrintWarning("Skipping resource bundling process; process requires at least 2 individual resource files.");
			return true;
		}

		string outputFilePath = Path.Combine(_dstAssetsAbsDir, "ResourcePackage.fres");

		return ResourceBundlingProcess.CombineResourcesFiles(_srcResourceMetadataPaths, outputFilePath, false);
	}

	private static bool ProcessOutput(List<string> _srcResourceFilePaths, string _srcResourceRootAbsDir, string _dstResourceRootAbsDir)
	{
		if (_srcResourceFilePaths.Count == 0)
		{
			PrintWarning("Skipping output copying process; process requires at least 1 resource file.");
			return true;
		}

		return CopyAssetsProcess.CopyAssetsToOutputDirectory(_srcResourceFilePaths, _srcResourceRootAbsDir, _dstResourceRootAbsDir);
	}

	public static void PrintStatus(string _message)
	{
		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = ConsoleColor.DarkGreen;
		Console.WriteLine(_message);
		Console.ForegroundColor = prevColor;
	}

	public static void PrintWarning(string _message)
	{
		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = ConsoleColor.DarkYellow;
		Console.WriteLine($"Warning: {_message}");
		Console.ForegroundColor = prevColor;
	}

	public static void PrintError(string _message, bool _addErrorPrefix = true)
	{
		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = ConsoleColor.Red;
		if (_addErrorPrefix)
		{
			Console.WriteLine($"Error! {_message}");
		}
		else
		{
			Console.WriteLine(_message);
		}
		Console.ForegroundColor = prevColor;
	}

	#endregion
}
