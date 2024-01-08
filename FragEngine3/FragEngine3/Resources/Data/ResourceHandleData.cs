
using FragEngine3.EngineCore;

namespace FragEngine3.Resources.Data
{
	[Serializable]
	public sealed class ResourceHandleData
	{
		#region Properties

		public string ResourceKey { get; set; } = string.Empty;
		public ResourceType ResourceType { get; set; } = ResourceType.Unknown;
		public EnginePlatformFlag PlatformFlags { get; set; } = EnginePlatformFlag.None;
		public string? ImportFlags { get; set; } = null;

		public ulong DataOffset { get; set; } = 0;
		public ulong DataSize { get; set; } = 0;

		public uint DependencyCount { get; set; } = 0;
		public string[]? Dependencies { get; set; } = null;

		#endregion
		#region Methods

		public bool IsValid() => !string.IsNullOrEmpty(ResourceKey) && ResourceType != ResourceType.Unknown && ResourceType != ResourceType.Ignored;

		#endregion
	}
}
