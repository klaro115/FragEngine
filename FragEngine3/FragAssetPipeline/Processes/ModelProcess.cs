using FragAssetFormats.Geometry.FMDL;
using FragAssetPipeline.Resources.Models;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Resources;
using FragEngine3.Resources.Data;

namespace FragAssetPipeline.Processes;

internal static class ModelProcess
{
	#region Fields

	private static readonly FModelExporter fmdlExporter = new();

	#endregion
	#region Properties

	public static bool IsInitialized { get; private set; } = false;

	public static ModelDataImporter? DataImporter { get; private set; } = null;

	#endregion
	#region Methods

	public static bool Initialize(ImporterContext _importCtx)
	{
		DataImporter = new(_importCtx);
		IsInitialized = true;
		return true;
	}

	public static void Shutdown()
	{
		IsInitialized = false;

		DataImporter?.Dispose();
		DataImporter = null;
	}

	public static bool PrepareResources(
		ImporterContext _exportCtx,
		string _inputFolderAbsPath,
		string _outputFolderAbsPath,
		List<string> _dstResourceFilePaths,
		IList<string>? preprocessedModelNames = null,
		IList<string>? alwaysPreprocessedModelFormats = null)
	{
		if (_exportCtx is null)
		{
			Console.WriteLine("Error! Cannot process 3D model resources using null exporter context!");
			return false;
		}
		if (!IsInitialized && !Initialize(_exportCtx))
		{
			Console.WriteLine("Error! Failed to initialize model process!");
			return false;
		}

		// Ensure input and output directories exist; create output if missing:
		if (!Directory.Exists(_inputFolderAbsPath))
		{
			_exportCtx.Logger.LogError($"Input directory for model process does not exist! Path: '{_inputFolderAbsPath}'");
			return false;
		}
		if (!Directory.Exists(_outputFolderAbsPath))
		{
			Directory.CreateDirectory(_outputFolderAbsPath);
		}

		string[] metadataFilePaths = Directory.GetFiles(_inputFolderAbsPath, "*.fres", SearchOption.AllDirectories);
		if (metadataFilePaths.Length == 0)
		{
			_exportCtx.Logger.LogWarning("Skipping model process; process requires at least 1 resource file in input directory.");
			return true;
		}

		// Process and output resources one after the other:
		int successCount = 0;
		int totalResourceCount = 0;

		foreach (string metadataFilePath in metadataFilePaths)
		{
			totalResourceCount++;

			try
			{
				if (ProcessModelResource(_exportCtx, _inputFolderAbsPath, _outputFolderAbsPath, metadataFilePath, preprocessedModelNames, alwaysPreprocessedModelFormats, out _, out string? outputMetadataFilePath))
				{
					_dstResourceFilePaths.Add(outputMetadataFilePath!);
					successCount++;
				}
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to process model resource! File path: '{metadataFilePath}'", ex);
				continue;
			}
		}

		// Print a brief summary of processing results:
		if (successCount < totalResourceCount)
		{
			_exportCtx.Logger.LogWarning($"Processing of {totalResourceCount - successCount}/{totalResourceCount} 3D models failed!");
		}
		else
		{
			_exportCtx.Logger.LogMessage($"Processing of all {totalResourceCount} 3D models succeeded.");
		}
		return successCount == totalResourceCount;
	}

	private static bool ProcessModelResource(
		ImporterContext _exportCtx,
		string _inputFolderAbsPath,
		string _outputFolderAbsPath,
		string metadataFilePath,
		IList<string>? preprocessedModelNames,
		IList<string>? alwaysPreprocessedModelFormats,
		out string? outputDataFilePath,
		out string? outputMetadataFilePath)
	{
		// Try reading contents of metadata file:
		if (!ResourceFileData.DeserializeFromFile(metadataFilePath, out ResourceFileData resMetadata))
		{
			_exportCtx.Logger.LogError($"Failed to deserialize metadata file for model resource! File path: '{metadataFilePath}'");
			outputDataFilePath = null;
			outputMetadataFilePath = null;
			return false;
		}

		// Determine if the file or format has been marked for FMDL-conversion:
		bool convertDataFile = false;
		string fileName = Path.GetFileNameWithoutExtension(metadataFilePath);
		if (preprocessedModelNames is not null && preprocessedModelNames.Contains(fileName))
		{
			convertDataFile = true;
		}

		string metaDataDirPath = Path.GetDirectoryName(metadataFilePath) ?? $".{Path.PathSeparator}";
		string dataFilePath = Path.GetFullPath(Path.Combine(metaDataDirPath, resMetadata.DataFilePath));
		string dataFileExt = Path.GetExtension(dataFilePath).ToLowerInvariant();
		if (alwaysPreprocessedModelFormats is not null && alwaysPreprocessedModelFormats.Contains(dataFileExt))
		{
			convertDataFile = true;
		}

		// Ensure output directory exists:
		string relativeOutputDirPath = Path.GetRelativePath(metaDataDirPath, _inputFolderAbsPath);
		string outputDirPath = Path.Combine(_outputFolderAbsPath, relativeOutputDirPath);
		if (!Directory.Exists(outputDirPath))
		{
			Directory.CreateDirectory(outputDirPath);
		}

		// Convert or copy output files:
		bool success = true;
		if (convertDataFile)
		{
			success &= ConvertResourceToFmdlFormat(_exportCtx, dataFilePath, metadataFilePath, outputDirPath, out outputDataFilePath, out outputMetadataFilePath);
		}
		else
		{
			success &= CopyResourceToDestination(_exportCtx, dataFilePath, metadataFilePath, outputDirPath, out outputDataFilePath, out outputMetadataFilePath);
		}
		return success;
	}

	private static bool ConvertResourceToFmdlFormat(
		ImporterContext _exportCtx,
		string? _srcDataFilePath,
		string? _srcMetadataFilePath,
		string _outputDirPath,
		out string? _outOutputDataFilePath,
		out string? _outOutputMetadataFilePath)
	{
		bool hasDataFilePath = !string.IsNullOrEmpty(_srcDataFilePath);
		bool hasMetadataFilePath = !string.IsNullOrEmpty(_srcMetadataFilePath);
		if (!hasDataFilePath && !hasMetadataFilePath)
		{
			_exportCtx.Logger.LogError("Cannot copy model resource files without data or metadata file paths!");
			_outOutputDataFilePath = null;
			_outOutputMetadataFilePath = null;
			return false;
		}

		// Determine whether a metadata file already exists; if not, create one later on:
		bool generateMetadataFile = false;
		string? srcFileName = _srcMetadataFilePath;

		if (!hasMetadataFilePath)
		{
			srcFileName = _srcDataFilePath;
			_srcMetadataFilePath = Path.ChangeExtension(_srcDataFilePath, ".fres");
			if (!File.Exists(_srcMetadataFilePath))
			{
				_srcMetadataFilePath = null;
				generateMetadataFile = true;
			}
		}
		// Check if data file is knwon; try to find a similarly named file if not:
		else if (!hasDataFilePath)
		{
			srcFileName = _srcMetadataFilePath;

			IEnumerator<string> e = ResourceFileConstants.EnumerateExtensionsForResourceType(ResourceType.Model);
			_srcDataFilePath = null;
			bool dataFileFound = false;

			while (e.MoveNext())
			{
				_srcDataFilePath = Path.ChangeExtension(_srcMetadataFilePath, e.Current);
				if (dataFileFound = File.Exists(_srcDataFilePath))
				{
					break;
				}
			}
			if (!dataFileFound)
			{
				_exportCtx.Logger.LogError($"Cannot find data file of model resource! File path: '{_srcMetadataFilePath}'");
				_outOutputDataFilePath = null;
				_outOutputMetadataFilePath = null;
				return false;
			}
		}

		// Try to determine resource key from metadata or data file name:
		string? resourceKey = null;
		if (hasMetadataFilePath && ResourceFileData.DeserializeFromFile(_srcMetadataFilePath!, out ResourceFileData fileHandleData))
		{
			if (fileHandleData.IsComplete())
			{
				resourceKey = fileHandleData.Resources![0].ResourceKey;
			}
			else
			{
				generateMetadataFile = true;
			}
		}
		if (string.IsNullOrEmpty(resourceKey))
		{
			string resourceKeyNameBase = hasMetadataFilePath ? _srcMetadataFilePath! : _srcDataFilePath!;
			resourceKey = Path.GetFileNameWithoutExtension(resourceKeyNameBase);
		}

		// Always recreate/update metadata file if the source format was not already FMDL:
		string srcDataFileExt = Path.GetExtension(_srcDataFilePath!).ToLowerInvariant();
		bool convertDataFile = srcDataFileExt != ".fmdl";
		if (convertDataFile)
		{
			generateMetadataFile = true;
		}

		srcFileName = Path.GetFileNameWithoutExtension(srcFileName);
		_outOutputDataFilePath = Path.Combine(_outputDirPath, $"{srcFileName}.fmdl");

		// Convert data file:
		if (convertDataFile)
		{
			// Import and parse surface data using a generic ASSIMP-based importer:
			if (!DataImporter!.ImportModelData(_srcDataFilePath!, resourceKey, null, out Dictionary<string, MeshSurfaceData>? subMeshDict))
			{
				_exportCtx.Logger.LogError($"Failed to import model surface data from source format!\nFile path: '{_srcDataFilePath}'");
				_outOutputMetadataFilePath = null;
				return false;
			}

			MeshSurfaceData? surfaceData = subMeshDict!.FirstOrDefault(o => o.Key == resourceKey).Value;	//TEMP
			if (surfaceData is null)
			{
				_exportCtx.Logger.LogError($"Failed to import model surface data for the requested sub-mesh!\nFile path: '{_srcDataFilePath}'");	//TEMP
				_outOutputMetadataFilePath = null;
				return false;
			}

			// Export to FMDL format:
			using FileStream outputDataFileStream = new(_outOutputDataFilePath, FileMode.Create, FileAccess.Write);
			if (!fmdlExporter.ExportModelData(_exportCtx, surfaceData!, outputDataFileStream))
			{
				_exportCtx.Logger.LogError($"Failed to export model surface data to FMDL format!\nFile path: '{_srcDataFilePath}'");
				_outOutputMetadataFilePath = null;
				return false;
			}
		}
		else
		{
			try
			{
				File.Copy(_srcDataFilePath!, _outOutputDataFilePath!, true);
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory! File path: '{_srcMetadataFilePath}'", ex);
				_outOutputMetadataFilePath = null;
				return false;
			}
		}

		// If missing or outdated, create metdata file now:
		bool success = true;
		if (generateMetadataFile)
		{
			success &= GenerateMetadataFile(_exportCtx, _outOutputDataFilePath, out _outOutputMetadataFilePath);
		}
		else
		{
			_outOutputMetadataFilePath = Path.ChangeExtension(_outOutputDataFilePath, ".fres");
			try
			{
				File.Copy(_srcMetadataFilePath!, _outOutputMetadataFilePath!, true);
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory!\nFile path: '{_srcMetadataFilePath}'", ex);
				success = false;
			}
		}
		return success;
	}

	private static bool CopyResourceToDestination(
		ImporterContext _exportCtx,
		string? _srcDataFilePath,
		string? _srcMetadataFilePath,
		string _outputDirPath,
		out string? _outOutputDataFilePath,
		out string? _outOutputMetadataFilePath)
	{
		if (string.IsNullOrEmpty(_srcDataFilePath) && string.IsNullOrEmpty(_srcMetadataFilePath))
		{
			_exportCtx.Logger.LogError("Cannot copy model resource files without data or metadata file paths!");
			_outOutputDataFilePath = null;
			_outOutputMetadataFilePath = null;
			return false;
		}

		// Determine whether a metadata file already exists; if not, create one later on:
		bool generateMetadataFile = false;

		if (string.IsNullOrWhiteSpace(_srcMetadataFilePath))
		{
			_srcMetadataFilePath = Path.ChangeExtension(_srcDataFilePath, ".fres");
			if (!File.Exists(_srcMetadataFilePath))
			{
				_srcMetadataFilePath = null;
				generateMetadataFile = true;
			}
		}
		// Check if data file is knwon; try to find a similarly named file if not:
		else if (string.IsNullOrWhiteSpace(_srcDataFilePath))
		{
			IEnumerator<string> e = ResourceFileConstants.EnumerateExtensionsForResourceType(ResourceType.Model);
			_srcDataFilePath = null;
			bool dataFileFound = false;
			while (e.MoveNext())
			{
				_srcDataFilePath = Path.ChangeExtension(_srcMetadataFilePath, e.Current);
				if (dataFileFound = File.Exists(_srcDataFilePath))
				{
					break;
				}
			}
			if (!dataFileFound)
			{
				_exportCtx.Logger.LogError($"Cannot find data file of model resource! File path: '{_srcMetadataFilePath}'");
				_outOutputDataFilePath = null;
				_outOutputMetadataFilePath = null;
				return false;
			}
		}

		// Copy data file:
		string srcDataFileName = Path.GetFileName(_srcDataFilePath!);
		_outOutputDataFilePath = Path.Combine(_outputDirPath, srcDataFileName);
		try
		{
			File.Copy(_srcDataFilePath!, _outOutputDataFilePath!, true);
		}
		catch (Exception ex)
		{
			_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory!\nFile path: '{_srcMetadataFilePath}'", ex);
			_outOutputMetadataFilePath = null;
			return false;
		}

		// If missing, create metdata file now:
		bool success = true;
		if (generateMetadataFile)
		{
			success &= GenerateMetadataFile(_exportCtx, _outOutputDataFilePath, out _outOutputMetadataFilePath);
		}
		else
		{
			_outOutputMetadataFilePath = Path.ChangeExtension(_outOutputDataFilePath, ".fres");
			try
			{
				File.Copy(_srcMetadataFilePath!, _outOutputMetadataFilePath!, true);
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory!\nFile path: '{_srcMetadataFilePath}'", ex);
				success = false;
			}
		}
		return success;
	}

	private static bool GenerateMetadataFile(ImporterContext _exportCtx, string _dataFilePath, out string? _outMetadataFilePath)
	{
		// Determine path of the metadata file:
		_outMetadataFilePath = Path.ChangeExtension(_dataFilePath, ".fres");
		if (File.Exists(_outMetadataFilePath))
		{
			return true;
		}

		string resourceKey = Path.GetFileNameWithoutExtension(_dataFilePath);
		string dataFileName = Path.GetFileName(_dataFilePath);
		string relativeDataFilePath = $"./{dataFileName}";

		// Calculate hash and measure size of output data file:
		if (!ResourceFileHandle.CalculateDataFileHash(_dataFilePath, out ulong dataFileHash, out ulong dataFileSize))
		{
			_exportCtx.Logger.LogError($"Failed to calculate hash and measure file size of model resource data file! File path: '{_dataFilePath}'");
			return false;
		}

		// Assemble metadata contents:
		ResourceHandleData resHandleData = new()
		{
			ResourceKey = resourceKey,
			ResourceType = ResourceType.Model,
			PlatformFlags = EnginePlatformFlag.None,
			ImportFlags = null,

			DataOffset = 0,
			DataSize = dataFileSize,

			DependencyCount = 0,
			Dependencies = null,
		};

		ResourceFileData resMetadata = new()
		{
			DataFilePath = relativeDataFilePath,
			DataFileType = ResourceFileType.Single,
			DataFileSize = dataFileSize,
			DataFileHash = dataFileHash,

			UncompressedFileSize = dataFileSize,
			BlockSize = 0,
			BlockCount = 0,

			ResourceCount = 1,
			Resources = [ resHandleData ],
		};

		// Serialize metadata to file and return success:
		bool success = resMetadata.SerializeToFile(_outMetadataFilePath);
		return success;
	}

	#endregion
}
