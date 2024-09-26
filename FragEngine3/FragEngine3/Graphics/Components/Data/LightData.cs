using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes.Data;

namespace FragEngine3.Graphics.Components.Data;

[ComponentDataType(typeof (LightComponent))]
public sealed class LightData
{
	#region Properties

	public LightType Type { get; set; } = LightType.Point;
	public uint LayerMask { get; set; } = 0xFFu;

	public string LightColor { get; set; } = "FFFFFFFF";
	public float LightIntensity { get; set; } = 1.0f;
	public float SpotAngleDegrees { get; set; } = 30.0f;

	public bool CastShadows {  get; set; } = false;
	public uint ShadowCascades { get; set; } = 0;
	public float ShadowBias { get; set; } = 0.02f;

	#endregion
	#region Methods

	public bool IsValid()
	{
		return
			LightIntensity >= 0 &&
			SpotAngleDegrees >= 0 && SpotAngleDegrees < 180.0f &&
			LightColor != null &&
			(LightColor.Length == 6 || LightColor.Length == 8);
	}

	#endregion
}
