using FragEngine3.EngineCore;
using FragEngine3.Resources.Data;
using FragEngine3.Resources.Management;
using System.IO.Compression;

namespace FragEngine3.Resources
{
	[Obsolete("Replaced")]
	public sealed class ResourceFileHandleOld
	{
		#region Types

		public sealed class ResourceInfo(string _resourceKey, ulong _resourceOffset = 0, ulong _resourceSize = 0)
		{
			public readonly string resourceKey = _resourceKey ?? string.Empty;
			public readonly ulong resourceOffset = _resourceOffset;
			public readonly ulong resourceSize = _resourceSize;
		}

		#endregion
		#region Constructors

		public ResourceFileHandleOld(ResourceManager _resourceManager, string _filePath, ResourceFileType _fileType, ResourceSource _fileSource, DateTime _lastModified, ResourceInfo[]? _resourceInfos, ulong _fileSize)
		{
			resourceManager = _resourceManager;
			filePath = _filePath ?? throw new ArgumentNullException(nameof(_filePath), "Resource file path may not be null!");
			fileType = _fileType;
			fileSource = _fileSource;
			uncompressedFileSize = _fileSize;
			lastModifiedDate = _lastModified;

			resourceInfos = _resourceInfos == null || _resourceInfos.Length == 0
				? ([ new(filePath, 0, 0) ])
				: _resourceInfos;
		}

		public ResourceFileHandleOld(ResourceManager _resourceManager, string _filePath, ResourceFileType _fileType, ResourceSource _fileSource, DateTime _lastModified, ResourceInfo[]? _resourceInfos, ulong _fileSize, uint _blockSize, uint _blockCount)
		{
			resourceManager = _resourceManager;
			filePath = _filePath ?? throw new ArgumentNullException(nameof(_filePath), "Resource file path may not be null!");
			fileType = _fileType;
			fileSource = _fileSource;
			uncompressedFileSize = _fileSize;
			blockSize = _blockSize;
			blockCount = _blockCount;
			lastModifiedDate = _lastModified;

			if (_resourceInfos == null || _resourceInfos.Length == 0)
			{
				resourceInfos = fileType == ResourceFileType.Single
					? ([ new(filePath, 0, 0) ])
					: throw new ArgumentException("Batched resource files must declare at least resource info!", nameof(_resourceInfos));
			}
			else
			{
				resourceInfos = _resourceInfos;
			}
		}

		private ResourceFileHandleOld(ResourceManager _resourceManager, ResourceFileMetadataOld _metadata, ResourceSource _fileSource, DateTime _lastModified)
		{
			if (_metadata == null) throw new ArgumentNullException(nameof(_metadata), "Resource file metadata may not be null!");

			resourceManager = _resourceManager;
			filePath = _metadata.DataFilePath;
			fileType = _metadata.FileType;
			fileSource = _fileSource;
			uncompressedFileSize = _metadata.UncompressedFileSize;
			blockSize = _metadata.BlockSize;
			blockCount = _metadata.BlockCount;
			lastModifiedDate = _lastModified;

			resourceInfos = new ResourceInfo[_metadata.GetResourceCount()];
			for (int i = 0; i < resourceInfos.Length; ++i)
			{
				ResourceHandleMetadataOld info = _metadata.Resources[i];
				resourceInfos[i] = new(info.ResourceName, info.ResourceOffset, info.ResourceSize);
			}
		}

		#endregion
		#region Fields

		public readonly ResourceManager resourceManager;

		// Data file metadata:
		public readonly string filePath = string.Empty;
		public readonly ResourceFileType fileType = ResourceFileType.Single;
		public readonly ResourceSource fileSource = ResourceSource.Core;
		public readonly ulong uncompressedFileSize = 0;
		public readonly uint blockSize = 0;
		public readonly uint blockCount = 0;
		public readonly DateTime lastModifiedDate;

		// Resource metadata:
		public readonly ResourceInfo[] resourceInfos = [];

		// State:
		public ResourceLoadState loadState = ResourceLoadState.NotLoaded;
		private byte[] dataBuffer = null!;

		private static readonly ResourceFileHandleOld none = new(null!, string.Empty, ResourceFileType.None, ResourceSource.Runtime, DateTime.UtcNow, null, 0);

		#endregion
		#region Properties

		/// <summary>
		/// A sorting key and identifier for this resource file.
		/// </summary>
		public string Key => filePath ?? string.Empty;
		public int ResourceCount => resourceInfos != null ? resourceInfos.Length : 0;

		public bool IsLoaded => (fileType != ResourceFileType.Batch_Compressed || dataBuffer != null) && loadState == ResourceLoadState.Loaded;
		public bool IsValid => resourceManager != null && !string.IsNullOrEmpty(Key) && ResourceCount != 0 && fileType != ResourceFileType.None;

		/// <summary>
		/// Returns an empty, invalid, and unassigned resource file handle.
		/// </summary>
		public static ResourceFileHandleOld None => none;

		private Logger Logger => resourceManager.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public static bool CreateFileHandle(ResourceManager _resourceManager, string _filePath, ResourceSource _fileSource, out ResourceFileHandleOld _outFileHandle, ref List<ResourceHandle> _outResourceHandles)
		{
			if (_resourceManager == null || _resourceManager.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot create file handle using null or disposed resource manager!");
				goto abort;
			}

			Logger logger = _resourceManager.engine.Logger ?? Logger.Instance!;
			if (string.IsNullOrWhiteSpace(_filePath))
			{
				logger.LogError("Cannot create file handle for null or blank file path!");
				goto abort;
			}
			if (!File.Exists(_filePath))
			{
				logger.LogError($"The resource file at path '{_filePath}' does not exist!");
				goto abort;
			}

			ResourceFileMetadataOld? metadata;
			DateTime lastModifiedUtc;

			// Locate the resource metadata/descriptor file first:
			if (ResourceFileFinder.GetMetadataFilePath(_filePath, out string metadataFilePath) && File.Exists(metadataFilePath))
			{
				if (!ResourceFileMetadataOld.DeserializeFromFile(metadataFilePath, out metadata))
				{
					logger.LogError($"Failed to load metadata for resource file at path '{_filePath}'!");
					goto abort;
				}
			}
			// If no metadata file exists, try generating it from the data file instead:
			else if (!ResourceFileFinder.GetMetadataFromDataFilePath(_filePath, out metadata))
			{
				logger.LogError($"Failed to generate metadata for resource data file at path '{_filePath}'!");
				goto abort;
			}

			if (metadata == null || !metadata.IsValid())
			{
				logger.LogError($"Error! Failed to retrieve metadata for resource file at path '{_filePath}'!");
				goto abort;
			}

			// For uncompressed data, complete or correct sizes and offsets as needed:
			try
			{
				FileInfo fileInfo = new(metadata.DataFilePath);

				lastModifiedUtc = fileInfo.LastWriteTimeUtc;

				if (!metadata.FileType.IsCompressed())
				{
					{
						metadata.UncompressedFileSize = (ulong)fileInfo.Length;
					}
					if (metadata.GetResourceCount() == 1)
					{
						metadata.Resources[0].ResourceOffset = 0;
						metadata.Resources[0].ResourceSize = metadata.UncompressedFileSize;
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogException($"The resource file at path '{metadata.DataFilePath}' could not be accessed!", ex);
				goto abort;
			}

			// Create file handle from metadata:
			_outFileHandle = new ResourceFileHandleOld(_resourceManager, metadata, _fileSource, lastModifiedUtc);
			if (!_outFileHandle.IsValid)
			{
				logger.LogError($"Resource metadata file at path '{_filePath}' is invalid or incomplete!");
				goto abort;
			}

			// Create resource handles from metadata:
			int resourceCount = metadata.GetResourceCount();
			if (resourceCount != 0)
			{
				_outResourceHandles ??= new List<ResourceHandle>(resourceCount);
				
				for (int i = 0; i < resourceCount; i++)
				{
					ResourceHandleMetadataOld resMetadata = metadata.Resources[i];
					if (resMetadata != null && resMetadata.IsValid())
					{
						//_outResourceHandles.Add(new ResourceHandle(_resourceManager, resMetadata, _outFileHandle));
					}
				}
			}
			return true;

		abort:
			_outFileHandle = none;
			return false;
		}

		/// <summary>
		/// Try to open a stream for reading raw uncompressed byte data for this resource file.<para/>
		/// NOTE: This may trigger full decompression of batched resource file.
		/// </summary>
		/// <param name="_dataOffset">Offset from where to start reading data from. This offset is measured in bytes of uncompressed data.</param>
		/// <param name="_dataSize">Size of the data that needs to be read using this stream. This size is measured in bytes of uncompressed data. Full file size is used if set to 0.</param>
		/// <param name="_outStream">Outputs a read-only stream of uncompressed raw data that resources can be imported/loaded/deserialized from. Null only if reading or decompression fails.</param>
		/// <returns>True if the stream could be opened and data can be read for loading, false otherwise.</returns>
		/// <exception cref="NotImplementedException"></exception>
		public bool TryOpenDataStream(ulong _dataOffset, ulong _dataSize, out Stream _outStream)
		{
			if (loadState == ResourceLoadState.Loaded && dataBuffer != null)
			{
				_outStream = new MemoryStream(dataBuffer);
				return true;
			}

			switch (fileType)
			{
				// Single resource file; just open a regular file stream directly:
				case ResourceFileType.Single:
					{
						_outStream = File.OpenRead(filePath);
						return true;
					}
				// Contiguously compressed batch file; read and decompress the whole file, buffer uncompressed data, then return requested data chunk:
				case ResourceFileType.Batch_Compressed:
					{
						if (!TryDecompressBatchedFile() || dataBuffer == null)
						{
							_outStream = null!;
							return false;
						}

						int startIdx = (int)_dataOffset < dataBuffer.Length
							? (int)_dataOffset
							: 0;
						int count = _dataSize != 0 && (int)_dataSize <= dataBuffer.Length - startIdx
							? (int)_dataSize
							: dataBuffer.Length - startIdx;

						_outStream = new MemoryStream(dataBuffer, startIdx, count, false);
						return true;
					}
				// Block-compressed batch file; read only blocks within the requested data byte range:
				case ResourceFileType.Batch_BlockCompressed:
					{
						throw new NotImplementedException("Block compression is not implemented yet.");
					}
				case ResourceFileType.None:
				default:
					break;
			}

			_outStream = null!;
			return false;
		}

		public bool TryReadResourceBytes(ResourceHandle _resourceHandle, out byte[] _outBytes)
		{
			if (_resourceHandle == null)
			{
				Logger.LogError("Cannot read data of null resource handle from file!");
				_outBytes = [];
				return false;
			}

			// Get data size and offset from handles:
			ulong fileOffset = 0;
			ulong fileSize = 0;
			if (fileType != ResourceFileType.Single)
			{
				fileOffset = _resourceHandle.fileOffset;
				fileSize = _resourceHandle.fileSize;
			}

			// Try reading byte data from file:
			Stream? stream = null!;
			byte[] bytes = [];
			try
			{
				if (TryOpenDataStream(fileOffset, fileSize, out stream))
				{
					int byteSize;
					if (fileSize == 0)
					{
						bytes = new byte[stream.Length];
						byteSize = stream.Read(bytes);
					}
					else
					{
						bytes = new byte[fileSize];
						byteSize = stream.Read(bytes, 0, (int)fileSize);
					}

					if (byteSize != bytes.Length)
					{
						byte[] trimmedBuffer = new byte[byteSize];
						Array.Copy(bytes, trimmedBuffer, byteSize);
						bytes = trimmedBuffer;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to read shader code from file!\nFile path: '{Key}'", ex);
				_outBytes = [];
				return false;
			}
			finally
			{
				stream?.Close();
			}

			_outBytes = bytes;
			return true;
		}

		/// <summary>
		/// Try to decompress the data of this resource file.<para/>
		/// WARNING: This method is only applicable to contiguously encoded (i.e. fileType = <see cref="ResourceFileType.Batch_Compressed"/>) batched resource files. It will fail for all other types.
		/// </summary>
		/// <returns>True if decompression was completed successfully, false otherwise. False if data file type is not '<see cref="ResourceFileType.Batch_Compressed"/>'.</returns>
		public bool TryDecompressBatchedFile()
		{
			if (loadState == ResourceLoadState.Loaded && dataBuffer != null)
			{
				return true;
			}

			if (fileType != ResourceFileType.Batch_Compressed)
			{
				Logger.LogError("File is not a contiguously compressed batch file!");
				return false;
			}
			if (string.IsNullOrEmpty(filePath))
			{
				Logger.LogError("File path may not be null or blank!");
				return false;
			}
			if (!File.Exists(filePath))
			{
				Logger.LogError($"File at path '{filePath}' does not exist!");
				return false;
			}

			try
			{
				using FileStream compressedStream = File.OpenRead(filePath);
				using DeflateStream decompressedStream = new(compressedStream, CompressionMode.Decompress);
				using MemoryStream resultStream = new();

				decompressedStream.CopyTo(resultStream);
				dataBuffer = resultStream.ToArray();

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to read and decompress batched resource file!", ex);
				return false;
			}
		}

		/// <summary>
		/// Discard any buffered uncompressed resource data. This method is called internally to free memory after all resources in the batch have been loaded.
		/// Calling it anywhere else may free up some system memory, but will also slow down import of further resources from this batch file.<para/>
		/// NOTE: This will reset the load state of contiguously compressed batched files to '<see cref="ResourceLoadState.NotLoaded"/>'.
		/// </summary>
		public void DiscardDecompressedDataBuffer()
		{
			dataBuffer = null!;
			if (fileType == ResourceFileType.Batch_Compressed)
			{
				loadState = ResourceLoadState.NotLoaded;
			}
		}

		public override string ToString()
		{
			return $"{filePath ?? "NULL"} ({fileType}, {loadState}) [Size: {uncompressedFileSize}, BS: {blockSize}, BC: {blockCount}]";
		}

		#endregion
	}
}
