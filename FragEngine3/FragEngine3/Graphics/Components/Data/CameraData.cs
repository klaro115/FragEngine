using FragEngine3.Scenes.Data;

namespace FragEngine3.Graphics.Components.Data
{
	[Serializable]
	[ComponentDataType(typeof(Camera))]
	public sealed class CameraData
	{
		#region Properties

		public int ResolutionX { get; set; } = 640;
		public int ResolutionY { get; set; } = 480;

		public float NearClipPlane { get; set; } = 0.001f;
		public float FarClipPlane { get; set; } = 1000.0f;
		public float FieldOfViewDegrees { get; set; } = 60.0f;

		#endregion
		#region Methods

		public bool IsValid()
		{
			return
				ResolutionX > 0 && ResolutionX < 8192 &&
				ResolutionY > 0 && ResolutionY < 8192 &&
				NearClipPlane > 0 &&
				NearClipPlane < FarClipPlane &&
				FarClipPlane < 100000.0f &&
				FieldOfViewDegrees > 0 && FieldOfViewDegrees < 180;
		}

		#endregion
	}
}
