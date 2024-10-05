using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using System.IO.Compression;

namespace FragAssetPipeline.Processes;

internal static class ResourceBundlingProcess
{
	#region Types

	private sealed class ResourceAndFilePair(string _dataFileAbsPath, ResourceHandleData _resourceData, ResourceFileData _fileData)
	{
		public readonly string dataFileAbsPath = _dataFileAbsPath;
		public readonly ResourceHandleData resourceData = _resourceData;
		public readonly ResourceFileData fileData = _fileData;
	}

	#endregion
	#region Methods

	public static bool CombineResourcesFiles(
		IEnumerable<string> _metadataFilePaths,
		string _outputFilePath,
		bool _compressDataFile,
		bool _useBlockCompression)
	{
		if (_metadataFilePaths is null || !_metadataFilePaths.Any())
		{
			Program.PrintError("Cannot combine null or empty set of resource files!");
			return false;
		}
		if (string.IsNullOrEmpty(_outputFilePath))
		{
			Program.PrintError("Cannot save combined resource files using nul or empty destination file path!");
			return false;
		}
		
		// Verify output directory, create if missing:
		string outputDir = Path.GetFullPath(Path.GetDirectoryName(_outputFilePath) ?? "./");
		string outputFileName = Path.GetFileNameWithoutExtension(_outputFilePath);
		if (!Directory.Exists(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}

		// Determine/Correct output file paths:
		string outputDataFileExtension = _useBlockCompression
			? ResourceConstants.FILE_EXT_BATCH_BLOCK_COMPRESSED
			: ResourceConstants.FILE_EXT_BATCH_NORMAL_COMPRESSED;
		string outputMetadataFilePath = Path.Combine(outputDir, $"{outputFileName}{ResourceConstants.FILE_EXT_METADATA}");
		string outputDataFilePath = Path.Combine(outputDir, $"{outputFileName}{outputDataFileExtension}");
		//string outputDataFileName = Path.GetFileName(outputDataFilePath);

		// Gather all resource handles and other info needed for the data merge:
		if (!GatherAllResourceHandles(
			_metadataFilePaths,
			out List<ResourceAndFilePair> srcResourceHandles,
			out ulong totalDataFileSize))
		{
			Program.PrintError($"Failed to gather and combine any resources for combined output file: '{_outputFilePath}'");
			return false;
		}

		// Merge all data files into one, and remap resource locations:
		if (!CombineDataFiles(
			outputDataFilePath,
			srcResourceHandles,
			_useBlockCompression,
			out ResourceHandleData[] dstResourceHandles,
			out ResourceFileType dstDataFileType,
			out ulong compressionBlockSize,
			out uint compressionBlockCount))
		{
			return false;
		}

		if (!ResourceFileHandle.CalculateDataFileHash(
			outputDataFilePath,
			out ulong dstDataFileHash,
			out ulong dstDataFileSize))
		{
			Program.PrintError($"Failed to calculate hash and measure file size for combined data file! Output path: '{outputDataFilePath}'");
			return false;
		}

		// Assemble resource metadata and serialize to file:
		ResourceFileData dstFileData = new()
		{
			DataFilePath = $"./outputDataFileName",
			DataFileType = dstDataFileType,
			DataFileSize = dstDataFileSize,
			DataFileHash = dstDataFileHash,

			UncompressedFileSize = totalDataFileSize,
			BlockSize = compressionBlockSize,
			BlockCount = compressionBlockCount,

			ResourceCount = dstResourceHandles.Length,
			Resources = dstResourceHandles,
		};
		return dstFileData.SerializeToFile(outputMetadataFilePath);
	}

	private static bool GatherAllResourceHandles(
		IEnumerable<string> _metadataFilePaths,
		out List<ResourceAndFilePair> _outSrcResourceHandles,
		out ulong _outTotalDataFileSize)
	{
		// Count any upcoming errors:
		int totalFileCount = _metadataFilePaths.Count();
		int successCount = 0;
		int errorCountNull = 0;
		int errorCountMissing = 0;
		int errorCountFormat = 0;
		int errorCountParse = 0;
		_outTotalDataFileSize = 0;

		// Gather all resource handles across all metadata files:
		Console.WriteLine($"+ Gathering resource handles from {totalFileCount} resource files...");

		_outSrcResourceHandles = new(totalFileCount);

		foreach (string metadataFilePath in _metadataFilePaths)
		{
			if (string.IsNullOrEmpty(metadataFilePath))
			{
				Program.PrintError("  * Resource file null.", false);
				errorCountNull++;
				continue;
			}
			string fileName = Path.GetFileName(metadataFilePath);
			if (!File.Exists(metadataFilePath))
			{
				Program.PrintError($"  * Resource file does not exist: '{fileName}'", false);
				errorCountMissing++;
				continue;
			}
			if (string.Compare(Path.GetExtension(metadataFilePath), ResourceConstants.FILE_EXT_METADATA, StringComparison.OrdinalIgnoreCase) != 0)
			{
				Program.PrintError($"  * Resource file uses unsupported file extension: '{fileName}'", false);
				errorCountFormat++;
				continue;
			}
			if (!ResourceFileData.DeserializeFromFile(metadataFilePath, out ResourceFileData fileData))
			{
				Program.PrintError($"  * Resource file could not be parsed: '{fileName}'", false);
				errorCountParse++;
				continue;
			}

			string metadataDirPath = Path.GetDirectoryName(metadataFilePath) ?? "./";
			string dataFileAbsPath = Path.GetFullPath(Path.Combine(metadataDirPath, fileData.DataFilePath));

			_outTotalDataFileSize += Math.Max(fileData.DataFileSize, fileData.UncompressedFileSize);
			successCount++;

			int resourceArrayCount = fileData.Resources is not null ? fileData.Resources.Length : 0;
			int actualResourceCount = Math.Min(fileData.ResourceCount, resourceArrayCount);
			for (int i = 0; i < actualResourceCount; ++i)
			{
				_outSrcResourceHandles.Add(new ResourceAndFilePair(dataFileAbsPath, fileData.Resources![i], fileData));
			}
		}

		// Print out error summary:
		if (errorCountNull != 0)
		{
			Program.PrintError($"{errorCountNull}/{totalFileCount} ({(float)errorCountNull / totalFileCount:0.#}%) resource files were null or empty!");
		}
		if (errorCountMissing != 0)
		{
			Program.PrintError($"{errorCountMissing}/{totalFileCount} ({(float)errorCountMissing / totalFileCount:0.#}%) resource files could not be found!");
		}
		if (errorCountFormat != 0)
		{
			Program.PrintError($"{errorCountFormat}/{totalFileCount} ({(float)errorCountFormat / totalFileCount:0.#}%) resource files were of unsupported file fomats!");
		}
		if (errorCountParse != 0)
		{
			Program.PrintError($"{errorCountParse}/{totalFileCount} ({(float)errorCountParse / totalFileCount:0.#}%) resource files could not be parsed!");
		}
		Console.WriteLine($"+ Resource metadata files read: {successCount}/{_outTotalDataFileSize} ({(float)successCount / totalFileCount:0.#}%)");
		Console.WriteLine($"+ Resource handles found: {_outSrcResourceHandles.Count} ({(float)_outSrcResourceHandles.Count / successCount:0.#} per file)");

		return successCount != 0 && _outSrcResourceHandles.Count != 0;
	}

	private static bool CombineDataFiles(
		string outputDataFilePath,
		List<ResourceAndFilePair> _srcResourceHandles,
		bool _useBlockCompression,
		out ResourceHandleData[] _outDstResourceHandles,
		out ResourceFileType _outDstDataFileType,
		out ulong _outCompressionBlockSize,
		out uint _outCompressionBlockCount)
	{
		// Prepare buffers and counters for combined file contents:
		_outDstResourceHandles = new ResourceHandleData[_srcResourceHandles.Count];

		_outDstDataFileType = _useBlockCompression
			? ResourceFileType.Batch_BlockCompressed
			: ResourceFileType.Batch_Compressed;

		_outCompressionBlockSize = 0;	//TODO [later]: implement block-compressed resource data files, then properly set these values here.
		_outCompressionBlockCount = 0;

		// Try combining data from all source data files into one contiguous compressed block:
		FileStream? fileStream = null;
		Stream? compressionStream = null;
		try
		{
			fileStream = new(outputDataFilePath, FileMode.Create, FileAccess.Write);
			compressionStream = new DeflateStream(fileStream, CompressionMode.Compress);	//TODO [later]: Use a block-compression stream instead if requested, and once implemented.

			ResourceFileData? currentFileData = null;
			byte[] srcDataFileBytes = [];
			int currentHandleIdx = 0;

			foreach (ResourceAndFilePair rfp in _srcResourceHandles)
			{
				if (rfp.fileData != currentFileData)
				{
					currentFileData = rfp.fileData;
					srcDataFileBytes = File.ReadAllBytes(rfp.dataFileAbsPath);
				}

				//TODO 1: Append source bytes to compression stream.
				//TODO 2: Update byte sizes and offsets in resource handle data.

				_outDstResourceHandles[currentHandleIdx] = rfp.resourceData;
			}
		}
		catch (Exception ex)
		{
			Program.PrintError($"Failed to combine resource data files into one! Output path: '{outputDataFilePath}'\nException: {ex}");
			return false;
		}
		finally
		{
			compressionStream?.Dispose();
			fileStream?.Dispose();
		}
		return true;
	}

	#endregion
}
