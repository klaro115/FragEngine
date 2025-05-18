using FragEngine3.EngineCore;
using FragEngine3.Graphics.ConstantBuffers;
using System.Numerics;

namespace FragEngine3.Graphics;

/// <summary>
/// Central registration for types and data layouts for use by the engine's graphics system,
/// </summary>
/// <param name="_engine">The engine instance that this was created for.</param>
public sealed class GraphicsTypeRegistry(Engine _engine)
{
	#region Fields

	private readonly Logger logger = _engine?.Logger ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

	private readonly Dictionary<string, Type> constantBufferTypeDict = new()
	{
		// Common vector/color/matrix types:
		[typeof(Vector4).FullName!] = typeof(Vector4),
		[typeof(Matrix4x4).FullName!] = typeof(Matrix4x4),

		// Engine CB types:
		[typeof(CBScene).FullName!] = typeof(CBScene),
		[typeof(CBCamera).FullName!] = typeof(CBCamera),
		[typeof(CBObject).FullName!] = typeof(CBObject),
		[typeof(CBDefaultSurface).FullName!] = typeof(CBDefaultSurface),

		//...
	};

	#endregion
	#region Methods

	/// <summary>
	/// Gets a read-only collection of all previously registered constant buffer data types.
	/// </summary>
	public IReadOnlyCollection<Type> GetAllConstantBufferTypes() => constantBufferTypeDict.Values;

	/// <summary>
	/// Registers an struct type that may be used as the CPU-side data representation of a constant buffer.
	/// </summary>
	/// <param name="_dataType">The new data type. This must be an unmanaged struct type.</param>
	/// <returns>True if the type was successfully registered. False on failure or if the type was already known.</returns>
	public bool RegisterConstantBufferType(Type _dataType)
	{
		if (_dataType is null)
		{
			logger.LogError("Cannot register null constant buffer data type!");
			return false;
		}
		if (!_dataType.IsValueType)
		{
			logger.LogError($"Cannot register constant buffer data type; type must be an unmanaged value type! (Type: '{_dataType.Name}')");
			return false;
		}

		string? fullTypeName = _dataType.FullName;
		if (string.IsNullOrEmpty(fullTypeName))
		{
			logger.LogError($"Cannot register constant buffer data type; unable to determine type name! (Type: '{_dataType.Name}')");
			return false;
		}

		if (!constantBufferTypeDict.TryAdd(fullTypeName, _dataType))
		{
			logger.LogError($"Constant buffer data type has already been registered! (Type name: '{fullTypeName}')");
			return false;
		}
		return true;
	}

	/// <summary>
	/// Tries to retrieve a constant buffer data type.
	/// </summary>
	/// <param name="_typeName">The full type name of the data type.<para/>
	/// NOTE: The type name refers to the type's full name, which can be retrieved via '<see cref="Type.FullName"/>'.</param>
	/// <param name="_outDataType">Outputs a previously registered type, or null, if no constant buffer type with this name has been registered.</param>
	/// <returns>True if a type of that name has been registered before, false otherwise.</returns>
	public bool TryGetConstantBufferDataType(string _typeName, out Type? _outDataType)
	{
		if (string.IsNullOrEmpty(_typeName))
		{
			logger.LogError("Type name of constant buffer data type may not be null or blank!");
			_outDataType = null;
			return false;
		}

		bool found = constantBufferTypeDict.TryGetValue(_typeName, out _outDataType);
		return found;
	}

	#endregion
}
