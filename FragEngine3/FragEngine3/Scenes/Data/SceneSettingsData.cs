using System.Numerics;

namespace FragEngine3.Scenes.Data
{
	[Serializable]
	public sealed class SceneSettingsData
	{
		#region Properties

		public Vector3 AmbientLightIntensity { get; set; } = new Vector3(0.1f, 0.1f, 0.1f);
		//...

		#endregion
	}
}
