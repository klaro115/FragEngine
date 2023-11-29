using FragEngine3.EngineCore;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Resources.Data
{
	[Serializable]
	public sealed class ResourceFileData
	{
		#region Properties

		// File details:
		public string DataFilePath { get; set; } = string.Empty;
		public ResourceFileType DataFileType { get; set; } = ResourceFileType.Single;
		public ulong DataFileSize { get; set; } = 0;

		// Compression details:
		public ulong UncompressedFileSize {  get; set; } = 0;
		public ulong BlockSize { get; set; } = 0;
		public uint BlockCount { get; set; } = 0;

		// Content details:
		public int ResourceCount { get; set; } = 0;
		public ResourceHandleData[]? Resources { get; set; } = null;

		private static readonly ResourceFileData none = new();

		#endregion
		#region Properties

		public static ResourceFileData None => none;

		#endregion
		#region Methods

		public bool IsValid() => !string.IsNullOrEmpty(DataFilePath) && ResourceCount != 0 && (DataFileType != ResourceFileType.Batch_BlockCompressed || (BlockSize != 0 && BlockCount != 0));
		public bool IsComplete() => IsValid() && DataFileSize > 0 && Resources != null && Resources.Length >= ResourceCount;

		/// <summary>
		/// Try to fill in any blanks or fix inconsistencies in file data. This may be necessary if '<see cref="IsComplete"/>' return false.
		/// If this method fails, any resources contained in and represented by this file are broken, missing, or corrupted, and cannot be
		/// loaded.
		/// </summary>
		/// <returns>True if some of the missing data could be recitified, false if the resource file is broken and unusable.</returns>
		public bool TryToCompleteData()
		{
			// Compression details cannot be reconstructed easily, abort if those are wrong:
			if (DataFileType != ResourceFileType.Single &&
				DataFileType != ResourceFileType.None)
			{
				if (UncompressedFileSize == 0 || UncompressedFileSize < DataFileSize)
					return false;
				if (DataFileType == ResourceFileType.Batch_BlockCompressed && (BlockSize == 0 || BlockCount == 0))
					return false;
			}

			// Ensure the resource count cannot exceed the number of listed resources:
            if (Resources != null && ResourceCount <= 0)
			{
				ResourceCount = Resources.Length;
			}
			// If resource is not defined, but there's only one, try deriving resource specs from file type:
			else if (Resources == null && DataFileType == ResourceFileType.Single && File.Exists(DataFilePath))
			{
				string fileExt = Path.GetExtension(DataFilePath);
				ResourceType resourceType = ResourceFileConstants.GetResourceTypeFromFileExtension(fileExt);

				if (resourceType != ResourceType.Unknown &&
					resourceType != ResourceType.Ignored)
				{
					ResourceCount = 1;
					Resources =
					[
						new ResourceHandleData()
						{
							ResourceKey = Path.GetFileName(DataFilePath),
							ResourceType = resourceType,
							DataOffset = 0,
							DataSize = 0,
						}
					];
				}
			}

			// If file size is missing, retrieve it from file:
			if (DataFileSize == 0 && !string.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
			{
				try
				{
					FileInfo fileInfo = new(DataFilePath);
					DataFileSize = (ulong)fileInfo.Length;
				}
				catch (Exception ex)
				{
					Logger.Instance?.LogException($"Failed to read file info of resource data file at path '{DataFilePath}'!", ex);
					return false;
				}
			}

			// For uncompressed single-resource data files, set uncompressed size to match file size: (not important, but more complete this way)
			if (DataFileSize != 0 && UncompressedFileSize != DataFileSize && DataFileType == ResourceFileType.Single)
			{
				UncompressedFileSize = DataFileSize;
			}

			return IsComplete();
        }

		public bool SerializeToFile(string _filePath) => Serializer.SerializeJsonToFile(this, _filePath);
		public static bool DeserializeFromFile(string _filePath, out ResourceFileData _outFileData) => Serializer.DeserializeJsonFromFile(_filePath, out _outFileData!);

		#endregion
	}
}
