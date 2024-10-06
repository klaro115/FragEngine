using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using System.IO.Compression;

namespace FragAssetPipeline.Processes;

/// <summary>
/// Utility class for bundling and combining multiple individual resource files into one compressed resource file.
/// </summary>
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

		// Gather all resource handles and other info needed for the data merge:
		if (!GatherAllResourceHandles(
			_metadataFilePaths,
			out List<ResourceAndFilePair> srcResourcesFilePairs,
			out ulong totalDataFileSize))
		{
			Program.PrintError($"Failed to gather and combine any resources for combined output file: '{_outputFilePath}'");
			return false;
		}

		// Merge all data files into one, and remap resource locations:
		if (!CombineDataFiles(
			outputDataFilePath,
			srcResourcesFilePairs,
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
		out List<ResourceAndFilePair> _outSrcResourceFilePairs,
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

		_outSrcResourceFilePairs = new(totalFileCount);

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
				_outSrcResourceFilePairs.Add(new ResourceAndFilePair(dataFileAbsPath, fileData.Resources![i], fileData));
			}
		}

		// Print out error summary:
		if (errorCountNull != 0)
		{
			Program.PrintError($"  - {errorCountNull}/{totalFileCount} ({(float)errorCountNull / totalFileCount * 100:0.#}%) resource files were null or empty!");
		}
		if (errorCountMissing != 0)
		{
			Program.PrintError($"  - {errorCountMissing}/{totalFileCount} ({(float)errorCountMissing / totalFileCount * 100:0.#}%) resource files could not be found!");
		}
		if (errorCountFormat != 0)
		{
			Program.PrintError($"  - {errorCountFormat}/{totalFileCount} ({(float)errorCountFormat / totalFileCount * 100:0.#}%) resource files were of unsupported file fomats!");
		}
		if (errorCountParse != 0)
		{
			Program.PrintError($"  - {errorCountParse}/{totalFileCount} ({(float)errorCountParse / totalFileCount * 100:0.#}%) resource files could not be parsed!");
		}
		Console.WriteLine($"  - Resource metadata files read: {successCount}/{totalFileCount} ({(float)successCount / totalFileCount * 100:0.#}%)");
		Console.WriteLine($"  - Resource handles found: {_outSrcResourceFilePairs.Count} ({(float)_outSrcResourceFilePairs.Count / successCount:0.#} per file)");

		return successCount != 0 && _outSrcResourceFilePairs.Count != 0;
	}

	private static bool CombineDataFiles(
		string outputDataFilePath,
		List<ResourceAndFilePair> _srcResourceFilePairs,
		bool _useBlockCompression,
		out ResourceHandleData[] _outDstResourceHandles,
		out ResourceFileType _outDstDataFileType,
		out ulong _outCompressionBlockSize,
		out uint _outCompressionBlockCount)
	{
		Console.WriteLine($"+ Combining resource data from {_srcResourceFilePairs.Count} source data files...");

		int totalResourceCount = _srcResourceFilePairs.Count();
		int successCount = 0;
		int errorCount = 0;

		// Prepare buffers and counters for combined file contents:
		_outDstResourceHandles = new ResourceHandleData[_srcResourceFilePairs.Count];

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

			ResourceFileData? loadedFileData = null;
			byte[] srcDataFileBytes = [];
			int currentHandleIdx = 0;
			ulong uncompressedByteSize = 0;

			foreach (ResourceAndFilePair rfp in _srcResourceFilePairs)
			{
				ResourceHandleData resHandle = rfp.resourceData;
				ResourceFileType dataFileType = rfp.fileData.DataFileType;

				// If source data file changes between resources, load its contents now:
				if (rfp.fileData != loadedFileData)
				{
					if (!LoadAndDecompressSourceFile(rfp, ref srcDataFileBytes))
					{
						Program.PrintError($"  * Failed to read source data file for resource key '{rfp.resourceData.ResourceKey}'!", false);
						errorCount++;
						continue;
					}
					loadedFileData = rfp.fileData;
				}

				// Append source bytes to compression stream:
				bool hasCopySucceeded = true;
				switch (dataFileType)
				{
					case ResourceFileType.Single:
					case ResourceFileType.Batch_Compressed:
						// For compressed files, append only the specific resource's data: (file was already decompressed during loading)
						if (resHandle.DataOffset != 0 && resHandle.DataSize != 0)
						{
							int dataSize = (int)resHandle.DataSize;
							compressionStream.Write(srcDataFileBytes, (int)resHandle.DataOffset, dataSize);

							rfp.resourceData.DataOffset = uncompressedByteSize;
							uncompressedByteSize += (ulong)dataSize;
						}
						// For single-resource files, just append uncompressed source data as-is:
						else
						{
							compressionStream.Write(srcDataFileBytes);

							rfp.resourceData.DataOffset = uncompressedByteSize;
							uncompressedByteSize += (ulong)srcDataFileBytes.Length;
						}
						break;
					case ResourceFileType.Batch_BlockCompressed:
						{
							//TODO [later]: Block (de)compression not implemented yet.
						}
						break;
					default:
						Program.PrintError($"  * Unsupported data file type '{dataFileType}' for resource key '{rfp.resourceData.ResourceKey}'!", false);
						hasCopySucceeded = false;
						errorCount++;
						break;
				}
				
				if (hasCopySucceeded)
				{
					_outDstResourceHandles[currentHandleIdx++] = rfp.resourceData;
					successCount++;
				}
			}
		}
		catch (Exception ex)
		{
			Program.PrintError($"Failed to combine resource data files into one! Output path: '{outputDataFilePath}'\nException: {ex}");
			return false;
		}
		finally
		{
			compressionStream?.Close();
			fileStream?.Close();
		}

		if (errorCount != 0)
		{
			Program.PrintError($"  - {errorCount}/{totalResourceCount} ({(float)errorCount / totalResourceCount * 100:0.#}%) resources could not be combined!");
		}
		Console.WriteLine($"  - Resources combined: {successCount}/{totalResourceCount} ({(float)successCount / totalResourceCount * 100:0.#}%)");
		return true;
	}

	private static bool LoadAndDecompressSourceFile(ResourceAndFilePair _resourceFilePair, ref byte[] _srcDataFileBytes)
	{
		// Read all bytes from source data file:
		try
		{
			_srcDataFileBytes = File.ReadAllBytes(_resourceFilePair.dataFileAbsPath);
		}
		catch (Exception ex)
		{
			Program.PrintError($"  * Failed to read source data file! File path: '{_resourceFilePair.fileData.DataFilePath}'!\nException: {ex}");
			return false;
		}

		// If source file is contîguously compressed, decompress file immediately: (block-compressed files are decompressed on-the-fly)
		if (_resourceFilePair.fileData.DataFileType == ResourceFileType.Batch_Compressed)
		{
			int uncompressedFileSize = Math.Max((int)_resourceFilePair.fileData.UncompressedFileSize, _srcDataFileBytes.Length);
			int actualUncompressedSize;
			byte[] decompressedDataFileBytes;

			// Try decompressing byte data:
			MemoryStream? decompressFileStream = null;
			DeflateStream? decompressionStream = null;

			try
			{
				decompressFileStream = new(_srcDataFileBytes);
				decompressionStream = new(decompressFileStream, CompressionMode.Decompress);

				decompressedDataFileBytes = new byte[uncompressedFileSize];
				actualUncompressedSize = decompressionStream.Read(decompressedDataFileBytes, 0, uncompressedFileSize);
			}
			catch (Exception ex)
			{
				Program.PrintError($"  * Failed to decompress source data file! File path: '{_resourceFilePair.fileData.DataFilePath}'!\nException: {ex}");
				return false;
			}
			finally
			{
				decompressionStream?.Close();
				decompressFileStream?.Close();
			}
			
			// Swap source data byte buffer with decompressed data:
			if (actualUncompressedSize != uncompressedFileSize)
			{
				_srcDataFileBytes = new byte[actualUncompressedSize];
				Array.Copy(decompressedDataFileBytes, _srcDataFileBytes, actualUncompressedSize);
			}
			else
			{
				_srcDataFileBytes = decompressedDataFileBytes;
			}
		}
		return true;
	}

	#endregion
}
