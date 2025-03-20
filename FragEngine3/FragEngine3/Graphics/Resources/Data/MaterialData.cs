using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data.MaterialTypes;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources.Data;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data;

/// <summary>
/// Serializable resource data that is used to serialize or describe a material resource.
/// </summary>
[Serializable]
[ResourceDataType(typeof(Material))]
[Obsolete("Rewritten")]
public sealed class MaterialData
{
	#region Properties

	/// <summary>
	/// The resource key identifying this material.
	/// </summary>
	public required string Key { get; init; } = string.Empty;
	/// <summary>
	/// The name of a type inheriting from <see cref="Material"/>, that this data shall be deserialized into. If null or empty, the base type is used instead.
	/// </summary>
	public string? TypeName { get; init; } = null;

	public MaterialStateData States { get; set; } = new();
	public MaterialShaderData Shaders { get; set; } = new();
	public MaterialReplacementData Replacements { get; set; } = new();
	public MaterialResourceData[]? Resources { get; set; } = null;
	public MaterialConstantBufferData[]? ConstantBuffers { get; set; } = null;		//TODO: Not used yet, needs to map to and write constant buffers.

	#endregion
	#region Methods

	/// <summary>
	/// Checks whether this material data appears to be complete and valid.
	/// </summary>
	public bool IsValid()
	{
		if (string.IsNullOrEmpty(Key)) return false;

		// All top-level data categories must be defined:
		if (States is null ||
			Shaders is null)
		{
			return false;
		}

		// If stencil is enabled, stencil behaviour may not be undefined:
		if (States.EnableStencil)
		{
			if (States.StencilFront is null ||
				States.StencilBack is null)
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

	/// <summary>
	/// Tries to create a description object for the resource layout required by this material.
	/// </summary>
	/// <param name="_outLayoutDesc">Outputs a description of resources that need to be bound to the pipeline for this material to work.
	/// This can be used to create <see cref="ResourceLayout"/> for rendering.</param>
	/// <param name="_outResourceKeysAndIndices">Outputs an array of keys for binding resources. These keys may be used to directly index
	/// and identify resource bindings and which slot a graphics resource (i.e. textures, device buffers, constant buffers, samplers) are
	/// bound to.</param>
	/// <param name="_outUseExternalBoundResources">Outputs whether this material uses any user-bound resources. If false, the material
	/// relies entirely on system-bound and internally managed resources.<para/>
	/// <returns>True if creating resource layout descriptions and keys succeeded, false otherwise.</returns>
	public bool GetBoundResourceLayoutDesc(out ResourceLayoutDescription _outLayoutDesc, out MaterialBoundResourceKeys[] _outResourceKeysAndIndices, out bool _outUseExternalBoundResources)
	{
		_outUseExternalBoundResources = false;
		if (Resources is null)
		{
			Logger.Instance?.LogError("Cannot create resource layout description using null resources array!");
			_outLayoutDesc = default;
			_outResourceKeysAndIndices = null!;
			return false;
		}

 		int resourceCount = Resources is not null ? Resources.Length : 0;

		if (resourceCount == 0)
		{
			Logger.Instance?.LogError("Cannot create resource layout description using empty resources array!");
			_outLayoutDesc = default;
			_outResourceKeysAndIndices = null!;
			return false;
		}

		// Assemble resource layout description and binding keys:
		ResourceLayoutElementDescription[] layoutElements = new ResourceLayoutElementDescription[resourceCount];
		_outResourceKeysAndIndices = new MaterialBoundResourceKeys[resourceCount];
		for (int i = 0; i < resourceCount; i++)
		{
			MaterialResourceData resData = Resources![i];

			layoutElements[i] = new ResourceLayoutElementDescription(
				resData.SlotName,
				resData.ResourceKind,
				resData.ShaderStageFlags);

			_outResourceKeysAndIndices[i] = new MaterialBoundResourceKeys(
				resData.ResourceKey ?? string.Empty,
				i,
				resData.SlotIndex,
				resData.ResourceKind,
				resData.Description);

			// If the data is flagged as such, or if there is a texture resource without a key, mark resource set as being bound by the system:
			if (resData.IsBoundBySystem || (string.IsNullOrEmpty(resData.ResourceKey) && resData.ResourceKind != ResourceKind.Sampler))
			{
				_outUseExternalBoundResources = true;
			}
		}

		_outLayoutDesc = new(layoutElements);
		return true;
	}

	#endregion
}
