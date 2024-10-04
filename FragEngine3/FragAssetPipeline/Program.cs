using FragAssetPipeline.Processes;
using FragAssetPipeline.Resources.Shaders;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using Veldrid;

namespace FragAssetPipeline;

internal static class Program
{
	#region Constants

	private const MeshVertexDataFlags flagsBasic = MeshVertexDataFlags.BasicSurfaceData;
	private const MeshVertexDataFlags flagsExt = MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData;

	#endregion
	#region Fields

	private static readonly ShaderProcessDetails[] details =
	[
		new("Basic_VS",                           "Basic_VS",                                       "Main_Vertex", ShaderStages.Vertex,   flagsExt),
		new("DefaultSurface_VS",                  "DefaultSurface_VS",                              "Main_Vertex", ShaderStages.Vertex,   flagsExt),
		new("DefaultSurface_modular_PS",          "DefaultSurface_modular_PS",                      "Main_Pixel",  ShaderStages.Fragment, flagsExt),
		new("AlphaShadow_PS",                     "shadows/AlphaShadow_PS",                         "Main_Pixel",  ShaderStages.Fragment, flagsExt),
		new("DefaultShadow_PS",				      "shadows/DefaultShadow_PS",                       "Main_Pixel",  ShaderStages.Fragment, flagsExt),
		new("ForwardPlusLight_CompositeScene_PS", "composition/ForwardPlusLight_CompositeScene_PS", "Main_Pixel",  ShaderStages.Fragment, flagsBasic),
		new("ForwardPlusLight_CompositeUI_PS",    "composition/ForwardPlusLight_CompositeUI_PS",    "Main_Pixel",  ShaderStages.Fragment, flagsBasic),
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

		PrintStatus("## PROCESSING SHADERS:");
		ProcessShaders();

		//...

		PrintStatus("\n## PROCESSING OUTPUT:");
		ProcessOutput();

		Console.WriteLine("\n#### END ####");
	}

	private static bool ProcessShaders()
	{
		const bool autoGenerateFresFiles = true;

		int successCount = 0;
		int totalShaderCount = details.Length;

		foreach (var detail in details)
		{
			if (!ShaderProcess.CompileShaderToFSHA(detail, in shaderConfig))
			{
				continue;
			}
			if (autoGenerateFresFiles && !ShaderProcess.GenerateResourceMetadataFile(detail))
			{
				continue;
			}

			successCount++;
		}

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

	private static bool ProcessOutput()
	{
		List<string> resourceFilePaths = [];

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

	public static void PrintError(string _message)
	{
		ConsoleColor prevColor = Console.ForegroundColor;
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Error! {_message}");
		Console.ForegroundColor = prevColor;
	}

	#endregion
}
