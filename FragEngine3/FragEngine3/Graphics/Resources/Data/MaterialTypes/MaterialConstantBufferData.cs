using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers;
using System.Runtime.InteropServices;

namespace FragEngine3.Graphics.Resources.Data.MaterialTypes;

/// <summary>
/// Serializable data class describing a value within a constant buffer.
/// </summary>
[Serializable]
public sealed class MaterialConstantBufferValue
{
	#region Properties

	/// <summary>
	/// Optional. The name of the variable this value should be assigned to.
	/// </summary>
	public string? Name { get; init; } = null;
	/// <summary>
	/// The sequential position index of the value within the constant buffer. The first variable has index 0, the 3rd
	/// one has index 2. It is assumed that all constant buffer data types use the <see cref="StructLayoutAttribute"/>,
	/// with either <see cref="LayoutKind.Sequential"/> or <see cref="LayoutKind.Explicit"/>.
	/// </summary>
	public required int Index { get; init; } = 0;
	/// <summary>
	/// The JSON-serialized value of the value.
	/// </summary>
	public required string SerializedValue { get; init; } = string.Empty;

	#endregion
	#region Methods

	public override string ToString()
	{
		return $"Name: '{Name}', Index: {Index}, Serialized value: '{SerializedValue ?? "NULL"}'";
	}

	#endregion
}

/// <summary>
/// Serializable data class describing a constant buffer and its contents.<para/>
/// NOTE: At least on of the properties '<see cref="Values"/>' and '<see cref="SerializedData"/>' must be defined for the
/// constant buffers types '<see cref="ConstantBufferType.Custom"/>' and '<see cref="ConstantBufferType.CBDefaultSurface"/>'.
/// For CBScene, CBCamera, and CBObject, all buffer contents and values will be handled internally by the engine.
/// </summary>
[Serializable]
public sealed class MaterialConstantBufferData
{
	#region Properties

	// BUFFER:

	/// <summary>
	/// The index of the resource slot that this constant buffer is bound to on the graphics pipeline.
	/// </summary>
	public uint SlotIndex { get; init; } = 0;
	/// <summary>
	/// The type and purpose of the constant buffer. User-defined buffers should use <see cref="ConstantBufferType.Custom"/>.
	/// </summary>
	public ConstantBufferType Type { get; init; } = ConstantBufferType.Custom;
	/// <summary>
	/// Custom buffer types only. Full type name of the unmanaged struct that represents the CPU-side contents of this
	/// constant buffer.
	/// </summary>
	public string? CustomTypeName {  get; init; } = null;

	// CONTENTS:

	/// <summary>
	/// An array of serialized values that the material can parse and assign on import.<para/>
	/// NOTE: This property is ignored by the material importer if <see cref="SerializedData"/> is non-null.
	/// </summary>
	public MaterialConstantBufferValue[]? Values { get; set; } = [];
	/// <summary>
	/// Optional. String-serialized data and settings that may be consumed by custom user-supplied material types.<para/>
	/// NOTE: If this is non-null, the material will try to deserialize buffer contents from this instead of parsing the
	/// <see cref="Values"/> array. Depending on material type, this string may be encoded as either JSON, XML, or Base64.
	/// </summary>
	public string? SerializedData { get; set; } = null;

	#endregion
	#region Methods

	/// <summary>
	/// Checks validity and completeness of this constant buffer data:
	/// </summary>
	/// <returns>True if valid and complete, false otherwise.</returns>
	public bool IsValid()
	{
		switch (Type)
		{
			case ConstantBufferType.CBScene:
			case ConstantBufferType.CBCamera:
			case ConstantBufferType.CBObject:
				break;
			case ConstantBufferType.CBDefaultSurface:
			case ConstantBufferType.Custom:
				// Either values or serialized data must be defined:
				if ((Values is null || Values.Length == 0) && string.IsNullOrEmpty(SerializedData))
				{
					return false;
				}
				break;
			default:
				// Invalid type:
				return false;
		}
		return true;
	}

	/// <summary>
	/// Tries to retrieve a CPU-side type that may be used to represent the constant buffer's data.
	/// </summary>
	/// <param name="_outDataType">Outputs the corresponding data type. Null if no fitting type was found.</param>
	/// <returns>True if a type could be identified, false otherwise.</returns>
	public bool TryGetDataType(out Type? _outDataType)
	{
		// Internal and default types can be resolved immediately:
		switch (Type)
		{
			case ConstantBufferType.CBScene:
				_outDataType = typeof(CBScene);
				return true;
			case ConstantBufferType.CBCamera:
				_outDataType = typeof(CBCamera);
				return true;
			case ConstantBufferType.CBObject:
				_outDataType = typeof(CBObject);
				return true;
			case ConstantBufferType.CBDefaultSurface:
				_outDataType = typeof(CBDefaultSurface);
				return true;
			case ConstantBufferType.Custom:
				break;
			default:
				// Invalid type:
				_outDataType = null;
				return false;
		}

		// Find custom CB data type by name:
		if (string.IsNullOrWhiteSpace(CustomTypeName))
		{
			_outDataType = null;
			return false;
		}

		try
		{
			_outDataType = System.Type.GetType(CustomTypeName, false, false);		//TODO [CRITICAL]: This is not good enough! Types outside of FragEngine3 assembly are not found! Add type registry service instead!
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to load constant buffer's custom data type! (Type name: '{CustomTypeName}')", ex, EngineCore.Logging.LogEntrySeverity.Trivial);
			_outDataType = null;
			return false;
		}

		// Perform a superficial check if this type is valid as CPU-side CB data:
		if (_outDataType is null)
		{
			Logger.Instance?.LogWarning($"Could not find custom constant buffer type '{CustomTypeName}'.");
			return false;
		}
		if (!_outDataType.IsValueType)
		{
			Logger.Instance?.LogWarning($"Custom constant buffer type '{CustomTypeName}' is not an unmanaged type.");
			return false;
		}

		return true;
	}

	public override string ToString()
	{
		return $"SlotIndex: {SlotIndex}, Type: '{Type}', Values: {(Values is not null ? Values.Length : 0)}x, Serialized data: '{SerializedData ?? "NULL"}'";
	}

	#endregion
}
