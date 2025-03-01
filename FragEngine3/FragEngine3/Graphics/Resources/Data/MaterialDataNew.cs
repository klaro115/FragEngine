using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers;
using FragEngine3.Graphics.Resources.Data.MaterialTypes;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources.Data;

namespace FragEngine3.Graphics.Resources.Data;

/// <summary>
/// Serializable resource data that is used to serialize or describe a material resource.
/// </summary>
[Serializable]
[ResourceDataType(typeof(Material))]
public sealed class MaterialDataNew
{
	#region Properties

	// GENERAL:

	/// <summary>
	/// The resource key identifying this material resource.
	/// </summary>
	public required string ResourceKey { get; init; } = string.Empty;
	/// <summary>
	/// The type of the material and its general purpose. Some data and resource bindings may be performed automatically.
	/// </summary>
	public required MaterialType MaterialType { get; init; }
	/// <summary>
	/// The name of a type inheriting from <see cref="Material"/>, that this data shall be deserialized into. If null or
	/// empty, a default material type is used instead.
	/// </summary>
	public string? TypeName { get; init; } = null;
	/// <summary>
	/// Optional. Additional string-serialized data and settings that may be consumed by custom user-supplied material types.
	/// </summary>
	public string? SerializedData { get; init; } = null;

	// BINDINGS:

	/// <summary>
	/// The shaders that this material should use for rendering.
	/// </summary>
	public MaterialShaderData Shaders { get; init; } = new();

	/// <summary>
	/// Optional. Replacement materials that should be used instead of this one, when rendering special cases.
	/// The most common replacement is the shadow material, which is used to draw depth and normals for shadow maps.
	/// If null, default materials will be used instead for shadows and LODs.
	/// </summary>
	public MaterialReplacementData? Replacements { get; init; } = null;

	/// <summary>
	/// An array of all constant buffers used by this material, including both system-provided and user-defined constants.
	/// </summary>
	public MaterialConstantBufferDataNew[]? Constants { get; init; } = null;

	/// <summary>
	/// An array of all texture, buffer, and sampler resources, excluding those managed internally by the engine.<para/>
	/// NOTE: Depending on material type, some system-provided resources are bound automatically and don't need to be
	/// listed here - data for them will be ignored on import.
	/// System-provided resources should only be listed for user-defined and specialty material types where they need to
	/// be bound manually.
	/// </summary>
	public MaterialResourceDataNew[]? Resources { get; init; } = null;

	#endregion
	#region Methods

	/// <summary>
	/// Checks validity and completeness of this material data:
	/// </summary>
	/// <returns>True if valid and complete, false otherwise.</returns>
	public bool IsValid(Logger? _logger = null, bool _logIssues = false)
	{
		if (_logIssues && _logger is null) _logger = Logger.Instance;

		if (string.IsNullOrEmpty(ResourceKey))
		{
			_logger?.LogWarning("Invalid MaterialData: Resource key for material data may not be null or blank.");
			return false;
		}
		if (Shaders is null)
		{
			_logger?.LogWarning($"Invalid MaterialData '{ResourceKey}': Shaders may not be null.");
			return false;
		}

		ConstantBufferType requiredConstantBufferFlags = 0;
		ConstantBufferType definedConstantBufferFlags = 0;

		// Depending on type, check resources:
		switch (MaterialType)
		{
			case MaterialType.Surface:
			case MaterialType.PostProcessing:
			case MaterialType.Compositing:
				// Vertex and pixel shaders are set:
				if (string.IsNullOrEmpty(Shaders.Vertex) ||
					string.IsNullOrEmpty(Shaders.Pixel))
				{
					_logger?.LogWarning($"Invalid MaterialData '{ResourceKey}': Material type '{MaterialType}' is missing vertex or pixel shaders.");
					return false;
				}
				// At least CBScene and CBCamera must be defined:
				requiredConstantBufferFlags = ConstantBufferType.CBScene | ConstantBufferType.CBCamera;
				break;
			case MaterialType.Compute:
				if (string.IsNullOrEmpty(Shaders.Compute))
				{
					_logger?.LogWarning($"Invalid MaterialData '{ResourceKey}': Material type '{MaterialType}' is missing a compute shader.");
					return false;
				}
				break;
			case MaterialType.UI:
				// At least CBCamera must be defined:
				requiredConstantBufferFlags = ConstantBufferType.CBCamera;
				break;
			default:
				// Invalid type:
				return false;
		}

		// Check constant buffer definitions:
		if (Constants is not null)
		{
			for (int i = 0; i < Constants.Length; i++)
			{
				MaterialConstantBufferDataNew? cbData = Constants[i];
				if (cbData is null || !cbData.IsValid())
				{
					_logger?.LogWarning($"Invalid MaterialData '{ResourceKey}': Constant buffer {i} is null or invalid.");
					return false;
				}
				definedConstantBufferFlags |= cbData.Type;
			}
		}
		if (!definedConstantBufferFlags.HasFlag(requiredConstantBufferFlags))
		{
			_logger?.LogWarning($"Invalid MaterialData '{ResourceKey}': Some required constant buffer definitions are missing. (Flags: {(int)definedConstantBufferFlags} vs. {(int)requiredConstantBufferFlags})");
			return false;
		}

		// Check bound resource definitions:
		if (Resources is not null)
		{
			for (int i = 0; i < Resources.Length;i++)
			{
				MaterialResourceDataNew? resData = Resources[i];
				if (resData is null || !resData.IsValid())
				{
					_logger?.LogWarning($"Invalid MaterialData '{ResourceKey}': Bound resource {i} is null or invalid.");
					return false;
				}
			}
		}

		return true;
	}

	public override string ToString()
	{
		int cbCount = Constants is not null ? Constants.Length : 0;
		int resCount = Resources is not null ? Resources.Length : 0;
		return $"MaterialData: {ResourceKey ?? "NULL"}, Type: {MaterialType}, TypeName: {TypeName ?? "NULL"}, Constant buffers: {cbCount}, Bound resources: {resCount}";
	}

	#endregion
}
