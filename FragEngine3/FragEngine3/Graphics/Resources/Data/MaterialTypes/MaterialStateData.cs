using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialStencilBehaviourData
{
	#region Properties

	public StencilOperation Fail { get; set; } = StencilOperation.Keep;
	public StencilOperation Pass { get; set; } = StencilOperation.Keep;
	public StencilOperation DepthFail { get; set; } = StencilOperation.Keep;
	public ComparisonKind ComparisonKind { get; set; } = ComparisonKind.LessEqual;

	#endregion
	#region Methods

	public StencilBehaviorDescription GetStencilBehaviourDesc()
	{
		StencilBehaviorDescription desc = new(
			Fail,
			Pass,
			DepthFail,
			ComparisonKind);
		return desc;
	}

	#endregion
}
