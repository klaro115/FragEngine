namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialDepthStencilStateData
{
	#region Properties

	// DEPTH:

	public bool EnableDepthTest { get; init; } = true;
	public bool EnableDepthWrite { get; init; } = true;

	// STENCIL:

	public bool EnableStencil { get; init; } = false;
	public MaterialStencilBehaviourData? StencilFront { get; init; } = null;
	public MaterialStencilBehaviourData? StencilBack { get; init; } = null;
	public byte StencilReadMask { get; init; } = 0;
	public byte StencilWriteMask { get; init; } = 0;
	public uint StencilReferenceValue { get; init; } = 0u;

	#endregion
}
