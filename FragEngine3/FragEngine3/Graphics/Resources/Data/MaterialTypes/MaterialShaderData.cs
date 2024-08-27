namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

[Serializable]
public sealed class MaterialShaderData
{
	#region Properties

	public bool IsSurfaceMaterial { get; set; } = true;

	public string Compute { get; set; } = string.Empty;

	public string Vertex { get; set; } = "DefaultSurface_VS";
	public string Geometry { get; set; } = string.Empty;
	public string TesselationCtrl { get; set; } = string.Empty;
	public string TesselationEval { get; set; } = string.Empty;
	public string Pixel { get; set; } = "DefaultSurface_PS";
	
	#endregion
}
