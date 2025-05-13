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
	#region Types

	private sealed class MeshResourceData
	{
		public required string ResourceKey { get; init; }
		public required string DataFilePath { get; init; }
		public required MeshSurfaceData? SurfaceData { get; init; }
	}

	public readonly struct OutputFilePaths(string _metadataFilePath, string _dataFilePath)
	{
		public readonly string metadataFilePath = _metadataFilePath;
		public readonly string dataFilePath = _dataFilePath;

		public readonly bool IsValid => !string.IsNullOrEmpty(metadataFilePath) && !string.IsNullOrEmpty(dataFilePath);

		public static OutputFilePaths None => new(string.Empty, string.Empty);
	}

	#endregion
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

			List<OutputFilePaths>? outputFilePaths;
			try
			{
				if (ProcessModelResource(_exportCtx, _inputFolderAbsPath, _outputFolderAbsPath, metadataFilePath, preprocessedModelNames, alwaysPreprocessedModelFormats, out outputFilePaths))
				{
					successCount++;
				}
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to process model resource! File path: '{metadataFilePath}'", ex);
				continue;
			}

			foreach (OutputFilePaths ofp in outputFilePaths!)
			{
				_dstResourceFilePaths.Add(ofp.metadataFilePath);
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
		out List<OutputFilePaths>? outputFilePaths)
	{
		// Try reading contents of metadata file:
		if (!ResourceFileData.DeserializeFromFile(metadataFilePath, out ResourceFileData resMetadata))
		{
			_exportCtx.Logger.LogError($"Failed to deserialize metadata file for model resource! File path: '{metadataFilePath}'");
			outputFilePaths = null;
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
			success &= ConvertResourceToFmdlFormat(_exportCtx, dataFilePath, metadataFilePath, outputDirPath, out outputFilePaths);
		}
		else
		{
			success &= CopyResourceToDestination(_exportCtx, dataFilePath, metadataFilePath, outputDirPath, out outputFilePaths);
		}
		return success;
	}

	private static bool ConvertResourceToFmdlFormat(
		ImporterContext _exportCtx,
		string? _srcDataFilePath,
		string? _srcMetadataFilePath,
		string _outputDirPath,
		out List<OutputFilePaths>? outputFilePaths)
	{
		bool hasDataFilePath = !string.IsNullOrEmpty(_srcDataFilePath);
		bool hasMetadataFilePath = !string.IsNullOrEmpty(_srcMetadataFilePath);
		if (!hasDataFilePath && !hasMetadataFilePath)
		{
			_exportCtx.Logger.LogError("Cannot copy model resource files without data or metadata file paths!");
			outputFilePaths = null;
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
				outputFilePaths = null;
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
		string outputDataFilePath = Path.Combine(_outputDirPath, $"{srcFileName}.fmdl");

		List<MeshResourceData> meshResourceData = [];

		// A) Convert data file:
		if (convertDataFile)
		{
			// Import and parse surface data using a generic ASSIMP-based importer:
			if (!DataImporter!.ImportModelData(_srcDataFilePath!, resourceKey, null, out Dictionary<string, MeshSurfaceData>? subMeshDict))
			{
				_exportCtx.Logger.LogError($"Failed to import model surface data from source format!\nFile path: '{_srcDataFilePath}'");
				outputFilePaths = null;
				return false;
			}
			generateMetadataFile |= subMeshDict!.Count > 1;

			foreach (var kvp in subMeshDict!)
			{
				string dataFilePath = Path.Combine(_outputDirPath, $"{kvp.Key}.fmdl");
				MeshResourceData subMeshData = new()
				{
					ResourceKey = kvp.Key,
					DataFilePath = dataFilePath,
					SurfaceData = kvp.Value,
				};
				meshResourceData.Add(subMeshData);
			}

			// Export sub-meshes to FMDL format:
			foreach (MeshResourceData subMeshData in meshResourceData)
			{
				using FileStream outputDataFileStream = new(subMeshData.DataFilePath, FileMode.Create, FileAccess.Write);

				if (!fmdlExporter.ExportModelData(_exportCtx, subMeshData.SurfaceData!, outputDataFileStream))
				{
					_exportCtx.Logger.LogError($"Failed to export model surface data to FMDL format!\nSub-Mesh: '{subMeshData.ResourceKey}'\nFile path: '{_srcDataFilePath}'");
					outputFilePaths = null;
					return false;
				}
			}
		}
		// B) Copy data file as-is:
		else
		{
			MeshResourceData data = new()
			{
				ResourceKey = resourceKey,
				DataFilePath = outputDataFilePath,
				SurfaceData = null,
			};
			meshResourceData.Add(data);

			try
			{
				File.Copy(_srcDataFilePath!, outputDataFilePath!, true);
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory! File path: '{_srcMetadataFilePath}'", ex);
				outputFilePaths = null;
				return false;
			}
		}

		outputFilePaths = new(meshResourceData.Count);

		// If missing or outdated, create metdata file now:
		bool success = true;
		if (generateMetadataFile)
		{
			foreach (MeshResourceData subMeshData in meshResourceData)
			{
				bool result = GenerateMetadataFile(_exportCtx, subMeshData.DataFilePath, out string? outputMetadataFilePath);
				if (result)
				{
					outputFilePaths.Add(new(outputMetadataFilePath!, subMeshData.DataFilePath));
				}
				success &= result;
			}
		}
		else
		{
			string outputMetadataFilePath = Path.ChangeExtension(outputDataFilePath, ".fres");
			try
			{
				File.Copy(_srcMetadataFilePath!, outputMetadataFilePath!, true);

				outputFilePaths.Add(new(outputMetadataFilePath, outputDataFilePath));
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
		out List<OutputFilePaths>? outputFilePaths)
	{
		if (string.IsNullOrEmpty(_srcDataFilePath) && string.IsNullOrEmpty(_srcMetadataFilePath))
		{
			_exportCtx.Logger.LogError("Cannot copy model resource files without data or metadata file paths!");
			outputFilePaths = null;
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
				outputFilePaths = null;
				return false;
			}
		}

		// Copy data file:
		string srcDataFileName = Path.GetFileName(_srcDataFilePath!);
		string outputDataFilePath = Path.Combine(_outputDirPath, srcDataFileName);
		string outputMetadataFilePath;
		try
		{
			File.Copy(_srcDataFilePath!, outputDataFilePath!, true);
		}
		catch (Exception ex)
		{
			_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory!\nFile path: '{_srcMetadataFilePath}'", ex);
			outputFilePaths = null;
			return false;
		}

		// If missing, create metdata file now:
		bool success = true;
		if (generateMetadataFile)
		{
			success &= GenerateMetadataFile(_exportCtx, outputDataFilePath, out outputMetadataFilePath!);
		}
		else
		{
			outputMetadataFilePath = Path.ChangeExtension(outputDataFilePath, ".fres");
			try
			{
				File.Copy(_srcMetadataFilePath!, outputMetadataFilePath!, true);
			}
			catch (Exception ex)
			{
				_exportCtx.Logger.LogException($"Failed to copy metadata file of model resource to output directory!\nFile path: '{_srcMetadataFilePath}'", ex);
				success = false;
			}
		}

		outputFilePaths =
		[
			new(outputMetadataFilePath, outputDataFilePath),
		];
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
