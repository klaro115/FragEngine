using FragEngine3.Graphics.ConstantBuffers;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FragEngine3.Utility;

/// <summary>
/// Helper class for determining the size of an unmanaged struct.
/// </summary>
public abstract class StructSizeHelper
{
	#region Constants

	/// <summary>
	/// Default name for constants that give the byte size of an unmanaged struct.
	/// </summary>
	public const string byteSizeConstantName = "byteSize";

	#endregion
	#region Methods

	/// <summary>
	/// Tries to determine the byte size of an unmanaged struct type.
	/// </summary>
	/// <param name="_structType"></param>
	/// <returns>The size of the given struct type, in bytes. Negative or zero on failure.</returns>
	/// <exception cref="ArgumentNullException">Struct type may not be null.</exception>
	/// <exception cref="ArgumentException">Type is not an unmanaged value type.</exception>
	/// <exception cref="Exception">Invalid identifiers, values, or failure to use reflection. Inner exceptions may provide additional details.</exception>
	public static int GetSizeOfStruct(Type _structType)
	{
		// Check if the given type is even valid for this:
		ArgumentNullException.ThrowIfNull(_structType);

		if (!_structType.IsValueType)
		{
			throw new ArgumentException("Type is not an unmanaged struct!", nameof(_structType));
		}

		// If the size is given via struct attribute, prefer that:
		StructLayoutAttribute? structLayout = _structType.StructLayoutAttribute;
		if (structLayout is not null && structLayout.Size > 0)
		{
			return structLayout.Size;
		}

		// If the data type is a standard engine-defined constant buffer type, use its size:
		ConstantBufferDataTypeAttribute? dataTypeAttribute = _structType.GetCustomAttribute<ConstantBufferDataTypeAttribute>();
		if (dataTypeAttribute is not null && dataTypeAttribute.constantBufferType != ConstantBufferType.Custom)
		{
			return dataTypeAttribute.constantBufferType switch
			{
				ConstantBufferType.CBScene => CBScene.packedByteSize,
				ConstantBufferType.CBCamera => CBCamera.packedByteSize,
				ConstantBufferType.CBObject => CBObject.packedByteSize,
				ConstantBufferType.CBDefaultSurface => CBDefaultSurface.packedByteSize,
				_ => throw new Exception($"Invalid constant buffer type '{dataTypeAttribute.constantBufferType}'!"),
			};
		}

		// If the data is a common scalar or vector type:
		if (GetSizeOfCommonType(_structType, out int byteSize))
		{
			return byteSize;
		}

		// Use reflection to check if the type has a "const int byteSize" member:
		try
		{
			FieldInfo? byteSizeConstField = _structType.GetField(byteSizeConstantName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (byteSizeConstField is not null)
			{
				object? value = byteSizeConstField.GetValue(null);
				if (value is int constByteSize)
				{
					return constByteSize;
				}
			}
		}
		catch (Exception ex)
		{
			throw new Exception($"Failed to reflect value of '{byteSizeConstantName}' constant!", ex);
		}

		return -1;
	}

	/// <summary>
	/// Tries to get the byte size of common scalar, vector, and matrix types used by the engine.
	/// </summary>
	/// <param name="_type">The type whose byte size we wish to determine.</param>
	/// <param name="_outSize">Outputs the size of an instance of the type, in bytes. Negative if the type is invalid or not common.</param>
	/// <returns>True if the given type is a known scalar or vector type, false otherwise.</returns>
	public static bool GetSizeOfCommonType(Type _type, out int _outSize)
	{
		if (_type is null)						_outSize = -1;

		// Logical:
		else if (_type == typeof(bool))			_outSize = sizeof(bool);

		// Integer scalars:
		else if (_type == typeof(byte))			_outSize = sizeof(byte);
		else if (_type == typeof(ushort))		_outSize = sizeof(ushort);
		else if (_type == typeof(uint))			_outSize = sizeof(uint);
		else if (_type == typeof(sbyte))		_outSize = sizeof(sbyte);
		else if (_type == typeof(short))		_outSize = sizeof(short);
		else if (_type == typeof(int))			_outSize = sizeof(int);
		else if (_type == typeof(char))			_outSize = sizeof(char);

		// Float scalars:
		else if (_type == typeof(float))		_outSize = sizeof(float);
		else if (_type == typeof(double))		_outSize = sizeof(double);
		else if (_type == typeof(Half))			_outSize = sizeof(ushort);

		// Vectors:
		else if (_type == typeof(Vector2))		_outSize = 2 * sizeof(float);
		else if (_type == typeof(Vector3))		_outSize = 3 * sizeof(float);
		else if (_type == typeof(Vector4))		_outSize = 4 * sizeof(float);
		else if (_type == typeof(Quaternion))	_outSize = 4 * sizeof(float);

		// Matrices:
		else if (_type == typeof(Matrix3x2))	_outSize = 6 * sizeof(float);
		else if (_type == typeof(Matrix4x4))	_outSize = 16 * sizeof(float);

		// Other:
		else _outSize = -1;

		return _outSize > 0;
	}

	#endregion
}
