using FragEngine3.EngineCore;

namespace FragEngine3.Resources.Data;

[Serializable]
[ResourceDataType(typeof(ResourceHandle))]
public sealed class ResourceHandleData
{
	#region Properties

	public string ResourceKey { get; init; } = string.Empty;
	public ResourceType ResourceType { get; init; } = ResourceType.Unknown;
	public EnginePlatformFlag PlatformFlags { get; init; } = EnginePlatformFlag.None;
	public string? ImportFlags { get; init; } = null;

	public ulong DataOffset { get; set; } = 0;
	public ulong DataSize { get; set; } = 0;

	public uint DependencyCount { get; init; } = 0;
	public string[]? Dependencies { get; init; } = null;

	#endregion
	#region Methods

	public bool IsValid() => !string.IsNullOrEmpty(ResourceKey) && ResourceType != ResourceType.Unknown && ResourceType != ResourceType.Ignored;

	#endregion
}
