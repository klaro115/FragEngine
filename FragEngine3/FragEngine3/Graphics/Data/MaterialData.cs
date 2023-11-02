using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Data
{
	[Serializable]
	public sealed class MaterialData
	{
		#region Types

		[Serializable]
		public sealed class StencilBehaviourData
		{
			public StencilOperation Fail { get; set; } = StencilOperation.Keep;
			public StencilOperation Pass { get; set; } = StencilOperation.Keep;
			public StencilOperation DepthFail { get; set; } = StencilOperation.Keep;
			public ComparisonKind ComparisonKind { get; set; } = ComparisonKind.LessEqual;
		}
		
		[Serializable]
		public sealed class StateData
		{
			public bool EnableDepthTest { get; set; } = true;
			public bool EnableDepthWrite { get; set; } = true;
			
			public bool EnableStencil { get; set; } = false;
			public StencilBehaviourData? StencilFront { get; set; } = null;
			public StencilBehaviourData? StencilBack { get; set; } = null;
			public byte StencilReadMask { get; set; } = 0;
			public byte StencilWriteMask { get; set; } = 0;
			public uint StencilReferenceValue { get; set; } = 0u;

			public bool EnableCulling { get; set; } = true;
		}

		[Serializable]
		public sealed class ShaderData
		{
			public bool IsSurfaceMaterial { get; set; } = true;

			public string Compute { get; set; } = string.Empty;

			public string Vertex { get; set; } = "DefaultSurface_VS";
			public string Geometry { get; set; } = string.Empty;
			public string TesselationCtrl { get; set; } = string.Empty;
			public string TesselationEval { get; set; } = string.Empty;
			public string Pixel { get; set; } = "DefaultSurface_PS";
		}

		[Serializable]
		public sealed class ResourceData
		{
			//TODO: Implement bound resources logic on material, then add references here.
		}

		#endregion
		#region Properties

		public string Key { get; set; } = string.Empty;

		public StateData States { get; set; } = new();
		public ShaderData Shaders { get; set; } = new();
		public ResourceData Resources { get; set; } = new();

		public MeshVertexDataFlags[] PreloadVariants { get; set; } = { MeshVertexDataFlags.BasicSurfaceData };

		#endregion
		#region Methods

		public bool IsValid()
		{
			if (string.IsNullOrEmpty(Key)) return false;

			// All top-level data categories must be defined:
			if (States == null ||
				Shaders == null ||
				Resources == null)
			{
				return false;
			}

			// If stencil is enabled, stencil behaviour may not be undefined:
			if (States.EnableStencil)
			{
				if (States.StencilFront == null ||
					States.StencilBack == null)
				{
					return false;
				}
			}

			// For non-compute shaders:
			if (Shaders.IsSurfaceMaterial || string.IsNullOrEmpty(Shaders.Compute))
			{
				// At least vertex and pixel shaders must be assigned:
				if (string.IsNullOrEmpty(Shaders.Vertex) ||
					string.IsNullOrEmpty(Shaders.Pixel))
				{
					return false;
				}
				// If either tesselation stage is defined, the other must be defined as well:
				if ((string.IsNullOrEmpty(Shaders.TesselationCtrl) && !string.IsNullOrEmpty(Shaders.TesselationEval)) ||
					(!string.IsNullOrEmpty(Shaders.TesselationCtrl) && string.IsNullOrEmpty(Shaders.TesselationEval)))
				{
					return false;
				}

				// For surface materials, make sure basic vertex data is a requirement for all pre-loaded variants:
				if (Shaders.IsSurfaceMaterial && PreloadVariants != null)
				{
					foreach (MeshVertexDataFlags variantFlags in PreloadVariants)
					{
						if (!variantFlags.HasFlag(MeshVertexDataFlags.BasicSurfaceData)) return false;
					}
				}
			}

			//...

			return true;
		}

		#endregion
	}
}

