using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialResourceData
{
	#region Properties

	public string ResourceKey { get; set; } = string.Empty;
	public string SlotName { get; set; } = string.Empty;
	public uint SlotIndex { get; set; } = 0;
	public ResourceKind ResourceKind { get; set; } = ResourceKind.TextureReadOnly;
	public ShaderStages ShaderStageFlags { get; set; } = ShaderStages.Fragment;
	public string? Description { get; set; } = null;
	public bool IsBoundBySystem { get; set; } = false;
	
	#endregion
}
