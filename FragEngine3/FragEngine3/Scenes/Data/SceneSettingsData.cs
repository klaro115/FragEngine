using System.Numerics;

namespace FragEngine3.Scenes.Data
{
	[Serializable]
	public sealed class SceneSettingsData
	{
		#region Properties

		public Vector3 AmbientLightIntensityLow { get; set; } = new Vector3(0.1f, 0.1f, 0.1f);
		public Vector3 AmbientLightIntensityMid { get; set; } = new Vector3(0.1f, 0.1f, 0.1f);
		public Vector3 AmbientLightIntensityHigh { get; set; } = new Vector3(0.1f, 0.1f, 0.1f);
		//...

		#endregion
	}
}
