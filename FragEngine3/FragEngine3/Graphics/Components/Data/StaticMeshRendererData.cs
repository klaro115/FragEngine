using System;

namespace FragEngine3.Graphics.Components.Data
{
	[Serializable]
	public sealed class StaticMeshRendererData
	{
		#region Properties

		public string Mesh { get; set; } = string.Empty;
		public string Material { get; set; } = string.Empty;

		#endregion
		#region Methods

		public bool IsValid()
		{
			return !string.IsNullOrEmpty(Mesh) && !string.IsNullOrEmpty(Material);
		}

		#endregion
	}
}
