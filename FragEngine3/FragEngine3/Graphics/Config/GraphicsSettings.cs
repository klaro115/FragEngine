using System.Numerics;

namespace FragEngine3.Graphics.Config
{
	[Serializable]
	public sealed class GraphicsSettings
	{
		#region Properties

		public bool Vsync { get; set; } = false;
		public Vector2 Resolution { get; set; } = new Vector2(640, 480);
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

		#endregion
	}
}

