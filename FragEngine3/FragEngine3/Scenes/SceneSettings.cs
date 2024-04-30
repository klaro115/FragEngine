using FragEngine3.Graphics;
using FragEngine3.Scenes.Data;
using System.Numerics;

namespace FragEngine3.Scenes
{
	public sealed class SceneSettings
	{
		#region Fields

		private Vector3 ambientLightIntensityLow = new(0.1f, 0.1f, 0.1f);
		private Vector3 ambientLightIntensityMid = new(0.1f, 0.1f, 0.1f);
		private Vector3 ambientLightIntensityHigh = new(0.1f, 0.1f, 0.1f);
		//...

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the global ambient light intensity for all renderers in the scene. Values cannot be negative.
		/// </summary>
		public Vector3 AmbientLightIntensity
		{
			get => 0.3333f * (ambientLightIntensityLow + ambientLightIntensityMid + ambientLightIntensityHigh);
			set
			{
				Vector3 clampedValue = Vector3.Clamp(value, Vector3.Zero, new(100.0f, 100.0f, 100.0f));
				ambientLightIntensityLow = clampedValue;
				ambientLightIntensityMid = clampedValue;
				ambientLightIntensityHigh = clampedValue;
			}
		}

		/// <summary>
		/// Gets or sets the ambient light radiating up from below, following the global +Y axis.
		/// </summary>
		public Vector3 AmbientLightIntensityLow
		{
			get => ambientLightIntensityLow;
			set => ambientLightIntensityLow = Vector3.Clamp(value, Vector3.Zero, new(100.0f, 100.0f, 100.0f));
		}

		/// <summary>
		/// Gets or sets the ambient light radiating across the horizontal plane, following the global X and Z axes.
		/// </summary>
		public Vector3 AmbientLightIntensityMid
		{
			get => ambientLightIntensityMid;
			set => ambientLightIntensityMid = Vector3.Clamp(value, Vector3.Zero, new(100.0f, 100.0f, 100.0f));
		}

		/// <summary>
		/// Gets or sets the ambient light radiating down from above, following the global -Y axis.
		/// </summary>
		public Vector3 AmbientLightIntensityHigh
		{
			get => ambientLightIntensityHigh;
			set => ambientLightIntensityHigh = Vector3.Clamp(value, Vector3.Zero, new(100.0f, 100.0f, 100.0f));
		}

		#endregion
		#region Methods

		public bool LoadData(in SceneSettingsData _data)
		{
			if (_data == null) return false;

			AmbientLightIntensityLow = (Vector3)Color32.ParseHexString(_data.AmbientLightIntensityLow);
			AmbientLightIntensityMid = (Vector3)Color32.ParseHexString(_data.AmbientLightIntensityMid);
			AmbientLightIntensityHigh = (Vector3)Color32.ParseHexString(_data.AmbientLightIntensityHigh);
			//...

			return true;
		}
		public bool SaveData(out SceneSettingsData _outData)
		{
			_outData = new()
			{
				AmbientLightIntensityLow = new Color32(ambientLightIntensityLow).ToHexString(),
				AmbientLightIntensityMid = new Color32(ambientLightIntensityMid).ToHexString(),
				AmbientLightIntensityHigh = new Color32(ambientLightIntensityHigh).ToHexString(),
				//...
			};
			return true;
		}

		#endregion
	}
}
