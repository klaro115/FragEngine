namespace FragAssetPipeline;

public static class ProgramConstants
{
	#region Constants Input

	// Asset source files, in the "Assets" folder in the repository's root directory:
	public const string inputRootRelativePath = "../../../../../";
	public const string inputAssetsRelativePath = inputRootRelativePath + "Assets/";

	#endregion
	#region Constants Output

	// Processed resource files, in the "data" folder in the repository's root directory:
	public const string outputRootRelativePath = "../../../../../";
	public const string outputAssetsRelativePath = outputRootRelativePath + "data/";

	#endregion
	#region Constants Build

	// Copied, ready-for-import resource files, in the "data" folder in the main app's build directory:
	private const string buildProjectName = "TestApp";
#if DEBUG
	private const string buildType = "Debug";
#else
	private string buildType = "Release";
#endif
	private const string buildNetVersionName = "net8.0";
	public const string buildRelativePath = $"../../../../{buildProjectName}/bin/{buildType}/{buildNetVersionName}/";

	#endregion
}
