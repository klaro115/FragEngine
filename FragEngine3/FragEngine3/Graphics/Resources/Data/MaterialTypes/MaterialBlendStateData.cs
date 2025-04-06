using Veldrid;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialBlendStateData
{
	#region Properties
	
	/// <summary>
	/// Whether to interprete alpha values of the output color as transparency. If true, blending will use alpha
	/// values to interpolate and mix color values when writing to the render target. If false, a homogenous blend
	/// will be applied across all pixels, using <see cref="BlendFactor"/> as per-channel blend weights.
	/// </summary>
	public bool AlphaIsTransparency {  get; init; } = true;

	/// <summary>
	/// A constant color value that is used for per-channel blending if <see cref="AlphaIsTransparency"/> is false.
	/// </summary>
	public RgbaFloat BlendFactor { get; init; } = RgbaFloat.White;

	/// <summary>
	/// An optional bias taht is applied when Z-sorting the renderers using this material when generating draw calls.
	/// This bias serves to reduce clipping or occlusion issues between transparent objects with complex geometry.
	/// </summary>
	public float ZSortingBias { get; init; } = 0.0f;
	
	#endregion
}

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

[Serializable]
public sealed class MaterialRasterizerStateData
{
	#region Properties

	/// <summary>
	/// Whether the rasterizer should perform culling of triangles if viewed from the wrong side.
	/// </summary>
	public bool EnableCulling { get; init; } = true;

	/// <summary>
	/// Whether to cull triangles in a clockwise sense. If false, culling uses counter-clockwise sense instead.
	/// </summary>
	public bool CullClockwise { get; init; } = false;

	#endregion
}
