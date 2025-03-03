using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Obsolete("Replaced")]
public readonly struct MaterialBoundResourceKeys(string _resourceKey, int _resourceIdx, uint _slotIdx, ResourceKind _resourceKind, string? _description)
{
	#region Fields

	public readonly string resourceKey = _resourceKey;
	public readonly int resourceIdx = _resourceIdx;
	public readonly uint slotIdx = _slotIdx;
	public readonly ResourceKind resourceKind = _resourceKind;
	public readonly string? description = _description;
	
	#endregion
}
