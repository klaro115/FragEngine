namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

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
