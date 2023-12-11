using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Config
{
	[Serializable]
	public sealed class GraphicsSettings
	{
		#region Properties

		// Output:
		public bool Vsync { get; set; } = false;
		public Vector2 Resolution { get; set; } = new Vector2(1280, 720);
		public int MsaaCount { get; set; } = 1;

		// Lighting:
		public uint MaxActiveLightCount { get; set; } = 128;

		#endregion
		#region Methods

		public GraphicsSettings Clone()
		{
			return new()
			{
				// Output:
				Vsync = Vsync,
				Resolution = Resolution,
				MsaaCount = MsaaCount,

				// Lighting:
				MaxActiveLightCount = MaxActiveLightCount,
			};
		}

		public TextureSampleCount GetTextureSampleCount()
		{
			return MsaaCount switch
			{
				1 => TextureSampleCount.Count1,
				2 => TextureSampleCount.Count2,
				4 => TextureSampleCount.Count4,
				8 => TextureSampleCount.Count8,
				16 => TextureSampleCount.Count16,
				32 => TextureSampleCount.Count32,
				_ => TextureSampleCount.Count1,
			};
		}

		#endregion
	}
}

