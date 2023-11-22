using FragEngine3.Scenes.Data;
using Veldrid;

namespace FragEngine3.Graphics.Components.Data
{
	[ComponentDataType(typeof (Light))]
	public sealed class LightData
	{
		#region Properties

		public Light.LightType Type { get; set; } = Light.LightType.Point;
		public uint LayerMask { get; set; } = 0xFFu;

		public RgbaFloat LightColor { get; set; } = RgbaFloat.White;
		public float LightIntensity { get; set; } = 1.0f;
		public float SpotAngleDegrees { get; set; } = 30.0f;

		#endregion
		#region Methods

		public bool IsValid()
		{
			return
				LightIntensity >= 0 &&
				SpotAngleDegrees >= 0 && SpotAngleDegrees < 180.0f;
		}

		#endregion
	}
}
