namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Obsolete("Rewritten")]
public sealed class MaterialConstantData
{
	#region Properties

	public string Name { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;

	#endregion
}

[Obsolete("Rewritten")]
public sealed class MaterialConstantBufferData
{
	#region Properties

	public string Name { get; set; } = string.Empty;
	public uint SlotIdx { get; set; } = 3;              // In HLSL, register slots b0 to b2 are reserved for system buffers. b3 is used for standard shader constants.

	public MaterialConstantData[]? Constants { get; set; } = [];

	#endregion
}
