using FragEngine3.Scenes.Data;

namespace FragEngine3.Graphics.Components.Data
{
	[Serializable]
	[ComponentDataType(typeof(Camera))]
	public sealed class CameraData
	{
		#region Properties

		// Resolution:
		public uint ResolutionX { get; set; } = 640u;
		public uint ResolutionY { get; set; } = 480u;

		// Projection:
		public float NearClipPlane { get; set; } = 0.001f;
		public float FarClipPlane { get; set; } = 1000.0f;
		public float FieldOfViewDegrees { get; set; } = 60.0f;

		// Content:
		public uint CameraPriority { get; set; } = 1000;
		public uint LayerMask { get; set; } = 0xFFFFFFFFu;

		// Clearing:
		public bool ClearBackground { get; set; } = true;
		public string ClearColor { get; set; } = "00000000";
		public float ClearDepth { get; set; } = 1.0f;
		public byte ClearStencil { get; set; } = 0x00;

		#endregion
		#region Methods

		public bool IsValid()
		{
			return
				ResolutionX < 8192 &&
				ResolutionY < 8192 &&
				NearClipPlane > 0 &&
				NearClipPlane < FarClipPlane &&
				FarClipPlane < 100000.0f &&
				FieldOfViewDegrees > 0 && FieldOfViewDegrees < 180 &&
				ClearColor != null && (ClearColor.Length == 6 || ClearColor.Length == 8);
		}

		#endregion
	}
}
