using FragEngine3.EngineCore;
using FragEngine3.Resources.Data;
using FragEngine3.Utility;

namespace FragEngine3.Resources.Management
{
	public static class ResourceFileFinder
	{
		#region Methods

		/// <summary>
		/// Get the file path for a hypothetical metadata/descriptor file. Uncompressed single-resource files do not require a descriptor; batched files must have one.<para/>
		/// NOTE: This only generates the file path where a metadata file should theoretically be located. No checks are done to see if such a file actually exists.
		/// </summary>
		/// <param name="_filePath">A path to some file - either the metadata/descriptor file, or a data file - pertaining to a resource file, or to a folder containing those files.</param>
		/// <param name="_outMetadataFilePath">Outputs a path to the metadata/descriptor file (ending in '.res') encoding a resource file handle. This file may not exist if the resource data
		/// is a single-resource file, but must exist for batched resource files.</param>
		/// <returns>True if a metadata file path could be generated from the given path, false otherwise.</returns>
		public static bool GetMetadataFilePath(string _filePath, out string _outMetadataFilePath)
		{
			if (string.IsNullOrEmpty(_filePath))
			{
				_outMetadataFilePath = string.Empty;
				return false;
			}

			// If the path has an extension, it must lead to a metadata or data file:
			if (Path.HasExtension(_filePath))
			{
				// Metadata file path:
				if (_filePath.EndsWith(ResourceConstants.FILE_EXT_METADATA, StringComparison.InvariantCultureIgnoreCase))
				{
					_outMetadataFilePath = _filePath;
					return true;
				}
				// Compressed data file paths:
				else if (_filePath.EndsWith(ResourceConstants.FILE_EXT_BATCH_NORMAL_COMPRESSED, StringComparison.InvariantCultureIgnoreCase) ||
					_filePath.EndsWith(ResourceConstants.FILE_EXT_BATCH_BLOCK_COMPRESSED, StringComparison.InvariantCultureIgnoreCase))
				{
					_outMetadataFilePath = Path.ChangeExtension(_filePath, ResourceConstants.FILE_EXT_METADATA);
					return true;
				}
				// Single-resource data file:
				else
				{
					_outMetadataFilePath = Path.ChangeExtension(_filePath, ResourceConstants.FILE_EXT_METADATA);
					return true;
				}
			}
			// If the path has no extension, it may be a folder containing both metadata and data files that belong together:
			else
			{
				string dirName = PathTools.GetLastPartName(_filePath);
				if (!string.IsNullOrEmpty(dirName))
				{
					_outMetadataFilePath = Path.Combine(_filePath, dirName + ResourceConstants.FILE_EXT_METADATA);
					return true;
				}
			}

			_outMetadataFilePath = string.Empty;
			return false;
		}

		/// <summary>
		/// Load resource file metadata from a file path.
		/// </summary>
		/// <param name="_dataFilePath">The file path leading to a resource data file. It can also handle metadata files, so don't worry about it too much,
		/// so long as the path unambiguously relates to a specific data/metadata file pair.</param>
		/// <param name="_outMetadata">Outputs the metadata for the resource file. This is either deserialized from file, or generated in case a single-resource
		/// data file does not have an accompanying metadata file.</param>
		/// <returns>True if metadata could be loaded or generated successfully, false otherwise.</returns>
		[Obsolete("Rewrite this")]
		public static bool GetMetadataFromDataFilePath(string _dataFilePath, out ResourceFileMetadataOld _outMetadata)
		{
			if (string.IsNullOrEmpty(_dataFilePath))
			{
				_outMetadata = ResourceFileMetadataOld.None;
				return false;
			}
			if (!File.Exists(_dataFilePath))
			{
				_outMetadata = ResourceFileMetadataOld.None;
				return false;
			}

			// Wait a second, this is not a data file! Parse it as a metadata file, you moron:
			if (_dataFilePath.EndsWith(ResourceConstants.FILE_EXT_METADATA, StringComparison.InvariantCultureIgnoreCase))
			{
				return ResourceFileMetadataOld.DeserializeFromFile(_dataFilePath, out _outMetadata);
			}
			// For block-compressed batched files, we need a dedicated metadata file. Cannot generate one, abort and return failure:
			else if (_dataFilePath.EndsWith(ResourceConstants.FILE_EXT_BATCH_BLOCK_COMPRESSED, StringComparison.InvariantCultureIgnoreCase))
			{
				Logger.Instance?.LogError("Cannot generate metadata for undocumented block-compressed batched data file!");
				_outMetadata = ResourceFileMetadataOld.None;
				return false;
			}

			// Create metadata assuming there is only one resource encoded within the data file:
			string resourceKey = PathTools.GetLastPartName(_dataFilePath);
			ResourceFileType fileType = _dataFilePath.EndsWith(ResourceConstants.FILE_EXT_BATCH_NORMAL_COMPRESSED, StringComparison.InvariantCultureIgnoreCase)
				? ResourceFileType.Batch_Compressed
				: ResourceFileType.Single;

			ResourceHandleMetadataOld singleResMetadata = new()
			{
				ResourceName = resourceKey,
				ResourceType = ResourceFileConstants.GetResourceTypeFromFilePath(_dataFilePath),
				ResourceOffset = 0,
				ResourceSize = 0,
				Dependencies = [],
			};

			_outMetadata = new()
			{
				DataFilePath = _dataFilePath,
				FileType = fileType,
				Resources = [ singleResMetadata ],
			};
			return _outMetadata.IsValid();
		}

		#endregion
	}
}
