﻿using FragEngine3.Resources.Data;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data
{
	[Serializable]
	[ResourceDataType(typeof(Material))]
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

			public RenderMode RenderMode { get; set; } = RenderMode.Opaque;
			public float ZSortingBias { get; set; } = 0.0f;
			public bool CastShadows { get; set; } = true;
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
		public sealed class ReplacementData
		{
			public string SimplifiedVersion { get; set; } = string.Empty;
			public string ShadowMap { get; set; } = string.Empty;
		}

		[Serializable]
		public sealed class BoundResourceData
		{
			public string ResourceKey { get; set; } = string.Empty;
			public string SlotName { get; set; } = string.Empty;
			public ResourceKind ResourceKind { get; set; } = ResourceKind.TextureReadOnly;
			public ShaderStages ShaderStageFlags { get; set; } = ShaderStages.Fragment;
			public bool IsBoundBySystem { get; set; } = false;
			public uint SlotIndex { get; set; } = 0;
		}

		[Serializable]
		public sealed class ResourceData
		{
			public int BoundResourceCount { get; set; } = 0;
			public BoundResourceData[]? BoundResources { get; set; } = null;
		}

		#endregion
		#region Properties

		public string Key { get; set; } = string.Empty;

		public StateData States { get; set; } = new();
		public ShaderData Shaders { get; set; } = new();
		public ReplacementData Replacements { get; set; } = new();
		public ResourceData Resources { get; set; } = new();

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
			// Depth bias for Z-sorting may not be NaN:
			if (float.IsNaN(States.ZSortingBias))
			{
				return false;
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
				if (string.IsNullOrEmpty(Shaders.TesselationCtrl) && !string.IsNullOrEmpty(Shaders.TesselationEval) ||
					!string.IsNullOrEmpty(Shaders.TesselationCtrl) && string.IsNullOrEmpty(Shaders.TesselationEval))
				{
					return false;
				}
			}

			//...

			return true;
		}

		public bool GetBoundResourceLayoutDesc(out ResourceLayoutDescription _outLayoutDesc, out Tuple<string, int>[] _outResourceKeysAndIndices, out bool _outUseExternalBoundResources)
		{
			_outUseExternalBoundResources = false;
			if (Resources == null)
			{
				_outLayoutDesc = default;
				_outResourceKeysAndIndices = null!;
				return false;
			}

			int resourceDataCount = Resources.BoundResources != null ? Resources.BoundResources.Length : 0;
			int resourceCount = Math.Min(Resources.BoundResourceCount, resourceDataCount);

			if (resourceCount == 0)
			{
				_outLayoutDesc = default;
				_outResourceKeysAndIndices = null!;
				return false;
			}

			ResourceLayoutElementDescription[] elements = new ResourceLayoutElementDescription[resourceCount];
			_outResourceKeysAndIndices = new Tuple<string, int>[resourceCount];
			for (int i = 0; i < resourceCount; i++)
			{
				BoundResourceData resData = Resources.BoundResources![i];
				elements[i] = new(resData.SlotName, resData.ResourceKind, resData.ShaderStageFlags);
				_outResourceKeysAndIndices[i] = new(resData.ResourceKey ?? string.Empty, i);
				if (string.IsNullOrEmpty(resData.ResourceKey) || resData.IsBoundBySystem)
				{
					_outUseExternalBoundResources = true;
				}
			}

			_outLayoutDesc = new(elements);
			return true;
		}

		#endregion
	}
}

