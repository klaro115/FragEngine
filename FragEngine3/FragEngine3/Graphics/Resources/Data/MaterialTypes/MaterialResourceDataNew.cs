using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialResourceDataNew
{
	#region Properties

	public string ResourceKey { get; init; } = string.Empty;
	public string SlotName { get; init; } = string.Empty;
	public uint SlotIndex { get; init; } = 0;
	public ResourceKind ResourceKind { get; init; } = ResourceKind.TextureReadOnly;
	public ShaderStages ShaderStageFlags { get; init; } = ShaderStages.Fragment;

	#endregion
	#region Methods

	/// <summary>
	/// Checks validity and completeness of this resource binding:
	/// </summary>
	/// <returns>True if valid and complete, false otherwise.</returns>
	public bool IsValid()
	{
		bool result =
			!string.IsNullOrEmpty(ResourceKey) &&
			!string.IsNullOrEmpty(SlotName) &&
			ShaderStageFlags != ShaderStages.None;
		return result;
	}

	public override string ToString()
	{
		return $"Slot index: {SlotIndex}, Slot name: {SlotName ?? "NULL"}, Type: '{ResourceKind}', Stages: '{ShaderStageFlags}', Resource key: '{ResourceKey ?? "NULL"}'";
	}

	#endregion
}
