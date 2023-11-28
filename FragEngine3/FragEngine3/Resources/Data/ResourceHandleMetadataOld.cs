using FragEngine3.Utility.Serialization;

namespace FragEngine3.Resources.Data
{
	[Obsolete]
	[Serializable]
	public sealed class ResourceHandleMetadataOld
	{
		#region Properties

		public string ResourceName { get; set; } = string.Empty;
		public ResourceType ResourceType { get; set; } = ResourceType.Unknown;
		public string[] Dependencies { get; set; } = Array.Empty<string>();

		public ulong ResourceOffset { get; set; } = 0;
		public ulong ResourceSize { get; set; } = 0;

		public static ResourceHandleMetadataOld None => new();

		#endregion
		#region Methods

		public bool IsValid() => !string.IsNullOrEmpty(ResourceName) && ResourceType != ResourceType.Unknown;

		public int GetDependencyCount() => Dependencies != null ? Dependencies.Length : 0;

		public static bool DeserializeSingleFromFile(string _metadataFilePath, out ResourceHandleMetadataOld _outMetadata) => Serializer.DeserializeJsonFromFile(_metadataFilePath, out _outMetadata!);
		public bool SerializeSingleToFile(string _metadataFilePath) => Serializer.SerializeJsonToFile(this, _metadataFilePath);

		#endregion
	}
}
