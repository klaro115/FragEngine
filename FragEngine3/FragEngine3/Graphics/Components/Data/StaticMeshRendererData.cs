using FragEngine3.Scenes.Data;

namespace FragEngine3.Graphics.Components.Data;

[Serializable]
[ComponentDataType(typeof(StaticMeshRenderer))]
public sealed class StaticMeshRendererData
{
	#region Properties

	public string Mesh { get; set; } = string.Empty;
	public string Material { get; set; } = string.Empty;
	public string ShadowMaterial { get; set; } = string.Empty;

	public bool DontDrawUnlessFullyLoaded { get; set; } = false;
	public uint LayerFlags { get; set; } = 1;

	#endregion
	#region Methods

	public bool IsValid()
	{
		return !string.IsNullOrEmpty(Mesh) && !string.IsNullOrEmpty(Material);
	}

	#endregion
}
