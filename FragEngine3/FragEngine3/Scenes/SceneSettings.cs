using FragEngine3.Scenes.Data;
using System.Numerics;

namespace FragEngine3.Scenes
{
	public sealed class SceneSettings
	{
		#region Fields

		private Vector3 ambientLightIntensity = new(0.1f, 0.1f, 0.1f);
		//...

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the global ambient light intensity for all renderers in the scene. Values cannot be negative.
		/// </summary>
		public Vector3 AmbientLightIntensity
		{
			get => ambientLightIntensity;
			set => ambientLightIntensity = Vector3.Clamp(value, Vector3.Zero, new(100.0f, 100.0f, 100.0f));
		}

		#endregion
		#region Methods

		public bool LoadData(in SceneSettingsData _data)
		{
			if (_data == null) return false;

			AmbientLightIntensity = _data.AmbientLightIntensity;
			//...

			return true;
		}
		public bool SaveData(out SceneSettingsData _outData)
		{
			_outData = new()
			{
				AmbientLightIntensity = ambientLightIntensity,
				//...
			};
			return true;
		}

		#endregion
	}
}
