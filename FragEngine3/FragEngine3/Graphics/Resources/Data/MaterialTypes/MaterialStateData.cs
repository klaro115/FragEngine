using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialStencilBehaviourData
{
	public StencilOperation Fail { get; set; } = StencilOperation.Keep;
	public StencilOperation Pass { get; set; } = StencilOperation.Keep;
	public StencilOperation DepthFail { get; set; } = StencilOperation.Keep;
	public ComparisonKind ComparisonKind { get; set; } = ComparisonKind.LessEqual;
}

[Serializable]
public sealed class MaterialStateData
{
	public bool EnableDepthTest { get; set; } = true;
	public bool EnableDepthWrite { get; set; } = true;

	public bool EnableStencil { get; set; } = false;
	public MaterialStencilBehaviourData? StencilFront { get; set; } = null;
	public MaterialStencilBehaviourData? StencilBack { get; set; } = null;
	public byte StencilReadMask { get; set; } = 0;
	public byte StencilWriteMask { get; set; } = 0;
	public uint StencilReferenceValue { get; set; } = 0u;

	public bool EnableCulling { get; set; } = true;

	public RenderMode RenderMode { get; set; } = RenderMode.Opaque;
	public float ZSortingBias { get; set; } = 0.0f;
}
