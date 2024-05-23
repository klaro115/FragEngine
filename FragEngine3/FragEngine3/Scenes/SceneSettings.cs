using FragEngine3.Scenes.Data;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Scenes;

public sealed class SceneSettings
{
	#region Fields

	private RgbaFloat ambientLightIntensityLow = new(0.1f, 0.1f, 0.1f, 0);
	private RgbaFloat ambientLightIntensityMid = new(0.1f, 0.1f, 0.1f, 0);
	private RgbaFloat ambientLightIntensityHigh = new(0.1f, 0.1f, 0.1f, 0);
	//...

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets the global ambient light intensity for all renderers in the scene. Values cannot be negative.
	/// </summary>
	public RgbaFloat AmbientLightIntensity
	{
		get
		{
			Vector4 averageColor = 0.3333f * (ambientLightIntensityLow.ToVector4() + ambientLightIntensityMid.ToVector4() + ambientLightIntensityHigh.ToVector4());
			return new(averageColor);
		}
		set
		{
			Vector4 clampedValue = Vector4.Clamp(value.ToVector4(), Vector4.Zero, new(100.0f, 100.0f, 100.0f, 0));
			RgbaFloat clampedColor = new(clampedValue);
			ambientLightIntensityLow = clampedColor;
			ambientLightIntensityMid = clampedColor;
			ambientLightIntensityHigh = clampedColor;
		}
	}

	/// <summary>
	/// Gets or sets the ambient light radiating up from below, following the global +Y axis.
	/// </summary>
	public RgbaFloat AmbientLightIntensityLow
	{
		get => new(ambientLightIntensityLow.R, ambientLightIntensityLow.G, ambientLightIntensityLow.B, 0);
		set
		{
			Vector4 clampedValue = Vector4.Clamp(value.ToVector4(), Vector4.Zero, new(100, 100, 100, 0));
			ambientLightIntensityLow = new(clampedValue);
		}
	}

	/// <summary>
	/// Gets or sets the ambient light radiating across the horizontal plane, following the global X and Z axes.
	/// </summary>
	public RgbaFloat AmbientLightIntensityMid
	{
		get => new(ambientLightIntensityMid.R, ambientLightIntensityMid.G, ambientLightIntensityMid.B, 0);
		set
		{
			Vector4 clampedValue = Vector4.Clamp(value.ToVector4(), Vector4.Zero, new(100, 100, 100, 0));
			ambientLightIntensityMid = new(clampedValue);
		}
	}

	/// <summary>
	/// Gets or sets the ambient light radiating down from above, following the global -Y axis.
	/// </summary>
	public RgbaFloat AmbientLightIntensityHigh
	{
		get => new(ambientLightIntensityHigh.R, ambientLightIntensityHigh.G, ambientLightIntensityHigh.B, 0);
		set
		{
			Vector4 clampedValue = Vector4.Clamp(value.ToVector4(), Vector4.Zero, new(100, 100, 100, 0));
			ambientLightIntensityHigh = new(clampedValue);
		}
	}

	#endregion
	#region Methods

	public bool LoadData(in SceneSettingsData _data)
	{
		if (_data == null) return false;

		AmbientLightIntensityLow = _data.AmbientLightIntensityLow;
		AmbientLightIntensityMid = _data.AmbientLightIntensityMid;
		AmbientLightIntensityHigh = _data.AmbientLightIntensityHigh;
		//...

		return true;
	}
	public bool SaveData(out SceneSettingsData _outData)
	{
		_outData = new()
		{
			AmbientLightIntensityLow = ambientLightIntensityLow,
			AmbientLightIntensityMid = ambientLightIntensityMid,
			AmbientLightIntensityHigh = ambientLightIntensityHigh,
			//...
		};
		return true;
	}

	#endregion
}
