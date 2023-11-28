using FragEngine3.Utility.Serialization;

namespace FragEngine3.Resources.Data
{
	[Obsolete]
	[Serializable]
	public sealed class ResourceFileMetadataOld
	{
		#region Constructors

		public ResourceFileMetadataOld() { }
		public ResourceFileMetadataOld(string _dataFilePath, ResourceFileType _fileType, ResourceHandleMetadataOld[] _resources, ulong _uncompressedFileSize, uint _blockSize = 0, uint _blockCount = 0)
		{
			DataFilePath = _dataFilePath;
			FileType = _fileType;
			UncompressedFileSize = _uncompressedFileSize;
			BlockSize = _blockSize;
			BlockCount = _blockCount;
			Resources = _resources;
		}

		#endregion
		#region Properties

		public string DataFilePath { get; set; } = string.Empty;
		public ResourceFileType FileType { get; set; } = ResourceFileType.None;
		public ulong UncompressedFileSize { get; set; } = 0;
		public uint BlockSize { get; set; } = 0;
		public uint BlockCount { get; set; } = 0;

		public ResourceHandleMetadataOld[] Resources { get; set; } = [];

		public static ResourceFileMetadataOld None => new();

		#endregion
		#region Methods

		public bool IsValid() => !string.IsNullOrEmpty(DataFilePath) && FileType != ResourceFileType.None;

		public int GetResourceCount() => Resources != null ? Resources.Length : 0;

		public static bool DeserializeFromFile(string _metadataFilePath, out ResourceFileMetadataOld _outMetadata) => Serializer.DeserializeJsonFromFile(_metadataFilePath, out _outMetadata!);

		public bool SerializeToFile(string _metadataFilePath) => Serializer.SerializeJsonToFile(this, _metadataFilePath);

		#endregion
	}
}
