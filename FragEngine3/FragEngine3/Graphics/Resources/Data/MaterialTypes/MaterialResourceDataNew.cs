using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialResourceDataNew
{
	#region Properties

	public string ResourceKey { get; set; } = string.Empty;
	public string SlotName { get; set; } = string.Empty;
	public uint SlotIndex { get; set; } = 0;
	public ResourceKind ResourceKind { get; set; } = ResourceKind.TextureReadOnly;
	public ShaderStages ShaderStageFlags { get; set; } = ShaderStages.Fragment;

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

	#endregion
}
