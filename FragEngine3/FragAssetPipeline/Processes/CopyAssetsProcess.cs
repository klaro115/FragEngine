using FragEngine3.Utility;

namespace FragAssetPipeline.Processes;

internal static class CopyAssetsProcess
{
	#region Methods

	/// <summary>
	/// Copies a bunch of resource files and their associated data files to an output directory for use by
	/// the main app.
	/// </summary>
	/// <param name="_outputDir">The target directory where all assets and resource files should be copy to.
	/// Additional child directories may be added to mirror the source directory's folder structure.</param>
	/// <param name="_resourceFilePaths">An enumerable of all resource descriptor files that need to be copied.
	/// This should only include files with the extension ".fres". Auxillary files not referenced directly
	/// in resource or file handles may be listed as well, to ensure that they are copied along with their
	/// associated resource data.</param>
	/// <returns>True if all assets were copied, false otherwise.</returns>
	public static bool CopyAssetsToOutputDirectory(string _outputDir, IEnumerable<string> _resourceFilePaths, string? _resourceFilesBasePath = null)
	{
		if (string.IsNullOrEmpty(_outputDir))
		{
			Console.WriteLine("Error! Cannot copy assets to null or blank output directory!");
			return false;
		}
		if (_resourceFilePaths is null)
		{
			Console.WriteLine("Error! Cannot copy null assets enumerable to output directory!");
			return false;
		}
		if (!Directory.Exists(_outputDir))
		{
			Console.WriteLine($"Error! Output directory for asset copy does not exist! Directory path: '{_outputDir}'");
			return false;
		}

		// Get the root directory that all paths are relative to, to reconstruct folder structure:
		string resourcesFilesCommonBasePath = PathTools.GetCommonBaseDirectoryPath(_resourceFilePaths);
		if (string.IsNullOrEmpty(_resourceFilesBasePath))
		{
			_resourceFilesBasePath = resourcesFilesCommonBasePath;
			resourcesFilesCommonBasePath = string.Empty;
		}
		else
		{
			resourcesFilesCommonBasePath = Path.GetRelativePath(_resourceFilesBasePath, resourcesFilesCommonBasePath);
		}

		// Prepare base folders within output directory:
		string outputBasePath = Path.Combine(_outputDir, resourcesFilesCommonBasePath);
		if (!string.IsNullOrEmpty(outputBasePath) && !Directory.Exists(outputBasePath))
		{
			Directory.CreateDirectory(outputBasePath);
		}

		//TODO 1: Copy resource files to relative destinations.
		//TODO 2: Gather and copy data files for each resource file.

		return true;
	}

	#endregion
}
