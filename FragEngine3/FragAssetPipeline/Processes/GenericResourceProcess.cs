using FragEngine3.Resources.Data;

namespace FragAssetPipeline.Processes;

internal static class GenericResourceProcess
{
	public static bool PrepareResources(string inputFolderAbsPath, string _outputFolderAbsPath, List<string> _dstResourceFilePaths)
	{
		// Ensure input and output directories exist; create output if missing:
		if (!Directory.Exists(inputFolderAbsPath))
		{
			Program.PrintError($"Input directory for generic resource process does not exist! Path: '{inputFolderAbsPath}'");
			return false;
		}
		if (!Directory.Exists(_outputFolderAbsPath))
		{
			Directory.CreateDirectory(_outputFolderAbsPath);
		}

		string[] metadataFilePaths = Directory.GetFiles(inputFolderAbsPath, "*.fres", SearchOption.AllDirectories);
		if (metadataFilePaths.Length == 0)
		{
			Program.PrintWarning("Skipping generic resource process; process requires at least 1 resource file in input directory.");
			return true;
		}

		// Process and output resources one after the other:
		int successCount = 0;
		int totalResourceCount = 0;

		foreach (string srcMetadataFilePath in metadataFilePaths)
		{
			// Skip resources that were already captured otherwise:
			if (_dstResourceFilePaths.Contains(srcMetadataFilePath)) continue;

			totalResourceCount++;

			if (!ResourceFileData.DeserializeFromFile(srcMetadataFilePath, out ResourceFileData fileData))
			{
				continue;
			}

			string srcMetadataDirPath = Path.GetDirectoryName(srcMetadataFilePath) ?? "./";
			string srcDataFilePath = Path.Combine(srcMetadataDirPath, fileData.DataFilePath);
			if (!File.Exists(srcDataFilePath))
			{
				continue;
			}

			string dstFolderDir = Path.Combine(_outputFolderAbsPath, Path.GetRelativePath(inputFolderAbsPath, srcMetadataDirPath));
			if (!Directory.Exists(dstFolderDir))
			{
				Directory.CreateDirectory(dstFolderDir);
			}

			string dstMetadataFilePath = Path.Combine(dstFolderDir, Path.GetFileName(srcMetadataFilePath));
			string dstDataFilePath = Path.Combine(dstFolderDir, Path.GetFileName(srcDataFilePath));

			File.Copy(srcMetadataFilePath, dstMetadataFilePath, true);
			File.Copy(srcDataFilePath, dstDataFilePath, true);

			_dstResourceFilePaths.Add(dstMetadataFilePath);
			successCount++;
		}

		// Print a brief summary of processing results:
		if (successCount < totalResourceCount)
		{
			Program.PrintWarning($"Processing of {totalResourceCount - successCount}/{totalResourceCount} generic resources failed!");
		}
		else
		{
			Console.WriteLine($"Processing of all {totalResourceCount} generic resources succeeded.");
		}
		return successCount == totalResourceCount;
	}
}
