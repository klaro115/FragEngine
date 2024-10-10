using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using FragEngine3.Utility;

namespace FragAssetPipeline.Processes;

internal static class CopyAssetsProcess
{
	#region Methods

	/// <summary>
	/// Copies a bunch of resource files and their associated data files to an output directory for use by
	/// the main app.
	/// </summary>
	/// <param name="_srcResourceFilePaths">An enumerable of all resource files that need to be copied.
	/// This should mostly include files with the extension ".fres", as data files are copied automatically along with
	/// their metadata files. Auxillary files not referenced directly in resource or file handles may be listed explicitly,
	/// to ensure that they are copied as well.</param>
	/// <param name="_srcResourceRootPath">The source directory that all resource files are copied from. This must be a
	/// base folder that is shared within the file paths of all given resource files. Folder structures will be recreated
	/// in destination folder to mimic resource files' locations relative to this base path.</param>
	/// <param name="_dstResourceRootPath">The target directory where all assets and resource files should be copied to.
	/// Additional child directories may be added to mirror the source directory's folder structure.</param>
	/// <returns>True if all assets were copied, false otherwise.</returns>
	public static bool CopyAssetsToOutputDirectory(IEnumerable<string> _srcResourceFilePaths, string? _srcResourceRootPath, string _dstResourceRootPath)
	{
		if (string.IsNullOrEmpty(_dstResourceRootPath))
		{
			Console.WriteLine("Error! Cannot copy assets to null or blank output directory!");
			return false;
		}
		if (_srcResourceFilePaths is null)
		{
			Console.WriteLine("Error! Cannot copy null assets enumerable to output directory!");
			return false;
		}
		if (!Directory.Exists(_dstResourceRootPath))
		{
			Console.WriteLine($"Error! Output directory for asset copy does not exist! Directory path: '{_dstResourceRootPath}'");
			return false;
		}

		// Don't do anything if no source files were given:
		if (!_srcResourceFilePaths.Any())
		{
			return true;
		}

		// Get the root directory that all paths are relative to, to reconstruct folder structure:
		if (string.IsNullOrEmpty(_srcResourceRootPath))
		{
			_srcResourceRootPath = PathTools.GetCommonBaseDirectoryPath(_srcResourceFilePaths);
		}
		else if (!Path.IsPathRooted(_srcResourceRootPath) && Path.IsPathRooted(_srcResourceFilePaths.First()))
		{
			_srcResourceRootPath = Path.GetFullPath(_srcResourceRootPath);
		}

		// Copy resource files to relative destinations:
		int totalFileCount = 0;
		int successCount = 0;
		int errorCountNull = 0;
		int errorCountNotFound = 0;
		int errorCountPath = 0;
		int errorCountParse = 0;

		foreach (string srcFilePath in _srcResourceFilePaths)
		{
			totalFileCount++;
			if (string.IsNullOrEmpty(srcFilePath))
			{
				errorCountNull++;
				continue;
			}
			if (!File.Exists(srcFilePath))
			{
				errorCountNotFound++;
				continue;
			}
			if (!GetOrCreateOutputPaths(srcFilePath, _srcResourceRootPath, _dstResourceRootPath, out string srcFileAbsPath, out string dstFileAbsPath))
			{
				errorCountPath++;
				continue;
			}

			File.Copy(srcFileAbsPath, dstFileAbsPath, true);
            successCount++;

			// Continue if the source is a metadata file that references a data file:
			string srcFileExt = Path.GetExtension(srcFilePath);
			if (string.Compare(srcFileExt, ResourceConstants.FILE_EXT_METADATA, StringComparison.InvariantCultureIgnoreCase) != 0)
			{
				continue;
			}

			if (!ResourceFileData.DeserializeFromFile(srcFileAbsPath, out ResourceFileData fileData))
			{
				errorCountParse++;
				continue;
			}

			string srcDirAbsPath = Path.GetDirectoryName(srcFileAbsPath)!;
			string srcDataFileAbsPath = Path.Combine(srcDirAbsPath, fileData.DataFilePath);

			if (!File.Exists(srcDataFileAbsPath))
			{
				errorCountNotFound++;
				continue;
			}
			if (!GetOrCreateOutputPaths(srcDataFileAbsPath, _srcResourceRootPath, _dstResourceRootPath, out _, out dstFileAbsPath))
			{
				errorCountPath++;
				continue;
			}

			File.Copy(srcFileAbsPath, dstFileAbsPath, true);
			successCount++;
		}

		//TODO: Log errors and success statistics.

		return true;
	}

	private static bool GetOrCreateOutputPaths(string _srcFileAbsPath, string _srcRootAbsPath, string _dstRootAbsPath, out string _outSrcFileAbsPath, out string _outDstFileAbsPath)
	{
		string srcRelativeFilePath = Path.GetRelativePath(_srcRootAbsPath, _srcFileAbsPath);
		bool isSrcLocatedInRootDir = string.Compare(srcRelativeFilePath, _srcFileAbsPath, StringComparison.InvariantCultureIgnoreCase) != 0;

		string dstFolderPath = isSrcLocatedInRootDir
			? Path.Combine(_dstRootAbsPath, Path.GetDirectoryName(srcRelativeFilePath) ?? string.Empty)
			: _dstRootAbsPath;
		string dstFolderAbsPath = Path.GetFullPath(dstFolderPath);

		if (!Directory.Exists(dstFolderAbsPath))
		{
			Directory.CreateDirectory(dstFolderAbsPath);
		}

		string srcFileName = Path.GetFileName(_srcFileAbsPath);

		_outSrcFileAbsPath = Path.GetFullPath(_srcFileAbsPath);
		_outDstFileAbsPath = Path.Combine(dstFolderAbsPath, srcFileName);
		return true;
	}

	#endregion
}
