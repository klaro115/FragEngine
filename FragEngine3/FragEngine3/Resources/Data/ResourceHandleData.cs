
namespace FragEngine3.Resources.Data
{
	[Serializable]
	public sealed class ResourceHandleData
	{
		#region Properties

		public string ResourceKey { get; set; } = string.Empty;
		public ResourceType ResourceType { get; set; } = ResourceType.Unknown;

		public ulong DataOffset { get; set; } = 0;
		public ulong DataSize { get; set; } = 0;

		#endregion
		#region Methods

		public bool IsValid() => !string.IsNullOrEmpty(ResourceKey) && ResourceType != ResourceType.Unknown && ResourceType != ResourceType.Ignored;

		#endregion
	}
}
