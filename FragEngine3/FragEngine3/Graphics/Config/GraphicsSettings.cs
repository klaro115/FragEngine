using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Config
{
	[Serializable]
	public sealed class GraphicsSettings
	{
		#region Properties

		public bool Vsync { get; set; } = false;
		public Vector2 Resolution { get; set; } = new Vector2(640, 480);
		public int MsaaCount { get; set; } = 1;
		//...

		#endregion
		#region Methods

		public GraphicsSettings Clone()
		{
			return new()
			{
				Vsync = Vsync,
				Resolution = Resolution,
				//...
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

