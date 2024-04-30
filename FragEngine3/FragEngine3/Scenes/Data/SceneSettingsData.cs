namespace FragEngine3.Scenes.Data;

[Serializable]
public sealed class SceneSettingsData
{
	#region Properties

	public string AmbientLightIntensityLow { get; set; } = "1A1A1AFF";
	public string AmbientLightIntensityMid { get; set; } = "1A1A1AFF";
	public string AmbientLightIntensityHigh { get; set; } = "1A1A1AFF";
	//...

	#endregion
}
