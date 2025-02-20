namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

/// <summary>
/// Data class containing resource keys for alternative materials that can replace this material in certain situations.
/// </summary>
[Serializable]
public sealed class MaterialReplacementData
{
	#region Properties

	/// <summary>
	/// Key for the simplified version of the material, which may be used on more distant LODs, or on lower graphics settings.
	/// </summary>
	public string SimplifiedVersion { get; set; } = string.Empty;
	/// <summary>
	/// Key for a material that should be used to draw shadow maps. The shadow materials needs to render only surface normals and depth.
	/// </summary>
	public string ShadowMap { get; set; } = string.Empty;
	
	#endregion
}
