using FragAssetPipeline.Processes;
using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragAssetPipeline;

internal static class Program
{
	#region Constants

	private static bool autoGenerateFresFiles = true;

	private const MeshVertexDataFlags flagsBasic = MeshVertexDataFlags.BasicSurfaceData;
	private const MeshVertexDataFlags flagsExt = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	#endregion
	#region Fields

	private static readonly ShaderProcess.Details[] details =
	[
		new("Basic_VS",                           "Basic_VS.hlsl",                                       "Main_Vertex", ShaderStages.Vertex,   flagsExt),
		new("DefaultSurface_VS",                  "DefaultSurface_VS.hlsl",                              "Main_Vertex", ShaderStages.Vertex,   flagsExt),
		new("DefaultSurface_modular_PS",          "DefaultSurface_modular_PS.hlsl",                      "Main_Pixel",  ShaderStages.Fragment, flagsExt),
		new("AlphaShadow_PS",                     "shadows/AlphaShadow_PS.hlsl",                         "Main_Pixel",  ShaderStages.Fragment, flagsExt),
		new("DefaultShadow_PS",                   "shadows/DefaultShadow_PS.hlsl",                       "Main_Pixel",  ShaderStages.Fragment, flagsExt),
		new("ForwardPlusLight_CompositeScene_PS", "composition/ForwardPlusLight_CompositeScene_PS.hlsl", "Main_Pixel",  ShaderStages.Fragment, flagsBasic),
		new("ForwardPlusLight_CompositeUI_PS",    "composition/ForwardPlusLight_CompositeUI_PS.hlsl",    "Main_Pixel",  ShaderStages.Fragment, flagsBasic),
	];

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

	#endregion
	#region Methods

	private static void Main(string[] args)
	{
		Console.WriteLine("### BEGIN ###\n");

		string? assetPipelineEntryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
		string assetPipelineBuildDir = Path.GetFullPath(assetPipelineEntryPath ?? Environment.CurrentDirectory);

		string inputAssetsAbsDir = Path.GetFullPath(Path.Combine(assetPipelineBuildDir, ProgramConstants.inputAssetsRelativePath));
		string outputAssetsAbsDir = Path.GetFullPath(Path.Combine(assetPipelineBuildDir, ProgramConstants.outputAssetsRelativePath));

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

		List<string> resourceFilePaths = [];

		PrintStatus("## PROCESSING SHADERS:");
		ProcessShaders(inputAssetsAbsDir, outputAssetsAbsDir, resourceFilePaths);

		//...

		PrintStatus("\n## PROCESSING OUTPUT:");
		ProcessOutput(resourceFilePaths);

		Console.WriteLine("\n#### END ####");
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
			if (!ShaderProcess.CompileShaderToFSHA(inputShaderDir, outputShaderDir, detail, in shaderConfig, out string dataFilePath))
			{
				continue;
			}
			// Optionally, generate a metadata file to go with the data file:
			if (autoGenerateFresFiles)
			{
				if (!ShaderProcess.GenerateResourceMetadataFile(inputShaderDir, outputShaderDir, detail, out string metadataFilePath))
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
			PrintWarning($"Processing of {successCount}/{totalShaderCount} shader resources failed!");
		}
		else
		{
			Console.WriteLine($"Processing of all {totalShaderCount} shader resources succeeded.");
		}
		return successCount == totalShaderCount;
	}

	private static bool ProcessOutput(List<string> _dstResourceFilePaths)
	{

		//TODO

		return true;
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
