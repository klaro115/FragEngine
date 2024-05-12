using Veldrid;

namespace FragEngine3.Scenes.Data;

/// <summary>
/// Serializable data type for scene settings of type '<see cref="SceneSettings"/>'.
/// </summary>
[Serializable]
public sealed class SceneSettingsData
{
	#region Properties

	public RgbaFloat AmbientLightIntensityLow { get; set; } = new(0.1f, 0.1f, 0.1f, 0);
	public RgbaFloat AmbientLightIntensityMid { get; set; } = new(0.1f, 0.1f, 0.1f, 0);
	public RgbaFloat AmbientLightIntensityHigh { get; set; } = new(0.1f, 0.1f, 0.1f, 0);
	//...

	#endregion
}
