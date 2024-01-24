using FragEngine3.EngineCore;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

[Serializable]
public sealed class ShaderGenVariable
{
	#region Fields

	private static readonly ShaderGenVariable none = new()
	{
		Name = string.Empty,
		BaseType = ShaderGenBaseDataType.Float,
		SizeX = 0,
		SizeY = 0,
		IsMutable = false,
	};

	#endregion
	#region Properties

	/// <summary>
	/// Variable name as it would appear in shader code. This name may be adjusted in code templates prior to insertion;
	/// usually a number suffix will be appended if the name collides with an existing variable of a mismatching type.
	/// It is recommended to choose output variables' names to directly reflect their origin. Input variable names may
	/// be generic, as they will likely be replaced in template code anyways following the suffix rules listed above.
	/// </summary>
	public string Name { get; set; } = string.Empty;
	/// <summary>
	/// If the variable is declared as part of templated code, this is the template name through which it's name may be
	/// found and replaced in templated source code. This name should be all caps and start with a '$' symbol. If null
	/// or blank, it is assumed that this variable is only ever used in a non-templated way.
	/// </summary>
	public string TemplatedName { get; set; } = string.Empty;

	public ShaderGenBaseDataType BaseType { get; set; } = ShaderGenBaseDataType.Float;
	/// <summary>
	/// Column count of the variable, In a vector type, this is the length of the vector, in a matrix, it's the first
	/// dimension right after the data type name, i.e. the 3 in "float3x4". For scalars, this value must be 1. For
	/// vectors and matrices, must be between 2 and 4.
	/// </summary>
	public uint SizeX { get; set; } = 1;
	/// <summary>
	/// Row count of the variable. In a matrix type, this is the second dimension after the x, i.e. the value 4 in
	/// "float3x4". For vectors and scalars, this value must be 1. For matrices, must be between 2 and 4.
	/// </summary>
	public uint SizeY { get; set; } = 1;
	/// <summary>
	/// Whether the variable is can be modified.<para/>
	/// INPUT: If the variable is an input, this denotes whether the feature's code may change the variable's value. Only
	/// non-const variables may be mapped to a mutable input of a feature.<para/>
	/// OUTPUT: If the variable is an output, this denotes whether the code declaring it marks it as const or read-only.
	/// The variable cannot be repurposed and its value can't be overwritten after it was returned by the declaring feature.
	/// </summary>
	public bool IsMutable { get; set; } = true;


	public static ShaderGenVariable None => none;

	#endregion
	#region Methods

	public static ShaderGenVariable CreateScalar(string _name, ShaderGenBaseDataType _baseType, bool _isMutable = true) => new()
	{
		Name = _name ?? string.Empty,
		BaseType = _baseType,
		SizeX = 1,
		SizeY = 1,
		IsMutable = _isMutable,
	};
	public static ShaderGenVariable CreateVector(string _name, ShaderGenBaseDataType _baseType, uint _sizeX, bool _isMutable = true) => new()
	{
		Name = _name ?? string.Empty,
		BaseType = _baseType,
		SizeX = Math.Clamp(_sizeX, 2, 4),
		SizeY = 1,
		IsMutable = _isMutable,
	};
	public static ShaderGenVariable CreateMatrix(string _name, ShaderGenBaseDataType _baseType, uint _sizeX, uint _sizeY, bool _isMutable = true) => new()
	{
		Name = _name ?? string.Empty,
		BaseType = _baseType,
		SizeX = Math.Clamp(_sizeX, 2, 4),
		SizeY = Math.Clamp(_sizeY, 2, 4),
		IsMutable = _isMutable,
	};

	public bool IsScalar() => SizeX == 1 && SizeY == 1;
	public bool IsVector() => SizeX > 1 && SizeY == 1;
	public bool IsMatrix() => SizeY > 1;

	/// <summary>
	/// Gets whether the variable's type is a scalar, vector, or matrix type.
	/// </summary>
	public ShaderGenTensorType GetTensorType()
	{
		if (SizeX > 1)
		{
			return SizeY > 1
				? ShaderGenTensorType.Matrix
				: ShaderGenTensorType.Vector;
		}
		return ShaderGenTensorType.Scalar;
	}

	/// <summary>
	/// Check whether this variable definition makes sense and will produce valid shader code.
	/// </summary>
	public bool IsValid()
	{
		return
			!string.IsNullOrEmpty(Name) &&
			BaseType != ShaderGenBaseDataType.None &&
			SizeX > 0 &&
			SizeY > 0 &&
			SizeX <= 4 &&
			SizeY <= 4 &&
			(IsScalar() || BaseType.IsNumericType());
	}

	/// <summary>
	/// Checks whether a first variable can be assigned to a second variable directly, without requiring a cast or vector padding.
	/// </summary>
	/// <param name="_first">The first variable, generally declared before the second variable. This is the source we want to get a value from.</param>
	/// <param name="_second">The second variable, generally declared or assigned after the first variable. This is the target or destination which we
	/// want to assign the first variable's value to.</param>
	/// <param name="_ignoreMutability">Whether to ignore the '<see cref="IsMutable"/>' flag of both variables. If false, a immutable/constant first
	/// variable cannot be connected to a second mutable variable. Set this to true if a mutable first variable should be assigned as const input to a
	/// shader feature.</param>
	/// <returns>True if the first variable's value can be directly mapped or assigned to the second variable, false otherwise.</returns>
	public static bool IsDirectlyCompatible(ShaderGenVariable _first, ShaderGenVariable _second, bool _ignoreMutability = false)
	{
		if (_first == null || _second == null) return false;
		if (ReferenceEquals(_first, _second)) return true;

		return
			string.CompareOrdinal(_first.Name, _second.Name) == 0 &&
			_first.BaseType == _second.BaseType &&
			_first.SizeX == _second.SizeX &&
			_first.SizeY == _second.SizeY &&
			(_ignoreMutability || _first.IsMutable == _second.IsMutable);
	}

	/// <summary>
	/// Checks whether a first variable can be assigned to a second variable after first casting and padding it to the second variable's type.
	/// </summary>
	/// <param name="_first">The first variable, generally declared before the second variable. This is the source we want to get a value from.</param>
	/// <param name="_second">The second variable, generally declared or assigned after the first variable. This is the target or destination which we
	/// want to assign the first variable's value to.</param>
	/// <param name="_ignoreMutability">Whether to ignore the '<see cref="IsMutable"/>' flag of both variables. If false, a immutable/constant first
	/// variable cannot be connected to a second mutable variable. Set this to true if a mutable first variable should be assigned as const input to a
	/// shader feature.</param>
	/// <returns>True if the first variable's value can be mapped or cast to the second variable, false otherwise.</returns>
	public static bool IsCompatibleAfterCasting(ShaderGenVariable _first, ShaderGenVariable _second, bool _ignoreMutability = false)
	{
		if (_first == null || _second == null) return false;
		if (ReferenceEquals(_first, _second)) return true;

		if (string.CompareOrdinal(_first.Name, _second.Name) != 0 ||
			(!_ignoreMutability && _first.IsMutable != _second.IsMutable))
		{
			return false;
		}

		return _first.GetTensorType() switch
		{
			ShaderGenTensorType.Scalar => !_second.IsMatrix() && (_first.BaseType.IsNumericType() || _first.BaseType == _second.BaseType),
			ShaderGenTensorType.Vector => _second.IsVector() && _second.SizeX >= _first.SizeX,
			ShaderGenTensorType.Matrix => _first.SizeX <= _second.SizeX && _first.SizeY == _second.SizeY,
			_ => false,
		};
	}

	/// <summary>
	/// Create the full type name used by a variable.<para/>
	/// EXAMPLE: BaseType=float, SizeX=3, SizeY=1, Output: "float3" | BaseType=half, SizeX=4, SizeY=3, Output: "half4x3"
	/// </summary>
	/// <param name="_dataType">The scalar base data type.</param>
	/// <param name="_sizeX">The number of elements/columns along the first axis. For scalar, this is 1, for vector/matrix, this is 2-4.</param>
	/// <param name="_sizeY">The number of elements/rows along the second axis. For scalar/vector, this is 1, for matrix, this is 2-4.</param>
	/// <param name="_dstBuilder">A string builder instance to write the type name into. The builder is never cleared by this method.</param>
	/// <returns>True if a valid name could be written to the string builder, false otherwise. No text is added on failure.</returns>
	public static bool CreateTypeName(ShaderGenBaseDataType _dataType, uint _sizeX, uint _sizeY, StringBuilder _dstBuilder)
	{
		if (_dstBuilder == null || _dataType == ShaderGenBaseDataType.None) return false;

		// Matrix:
		if (_sizeY > 1 && _dataType.IsNumericType())
		{
			_dstBuilder.Append(_dataType.GetBaseName()).Append(_sizeX).Append('x').Append(_sizeY);
		}
		// Vector:
		else if (_sizeX > 1 && _dataType.IsNumericType())
		{
			_dstBuilder.Append(_dataType.GetBaseName()).Append(_sizeX);
		}
		// Scalar:
		else
		{
			_dstBuilder.Append(_dataType.GetBaseName());	
		}
		return true;
	}
	/// <summary>
	/// Create the full type name used by this variable.
	/// </summary>
	/// <param name="_dstBuilder">A string builder instance to write the type name into. The builder is never cleared by this method.</param>
	/// <returns>True if a valid name could be written to the string builder, false otherwise. No text is added on failure.</returns>
	public bool CreateTypeName(StringBuilder _dstBuilder)
	{
		return CreateTypeName(BaseType, SizeX, SizeY, _dstBuilder);
	}

	/// <summary>
	/// Creates code for a casting operation, assigning a first variable's value to a second variable declaration that uses a different type or
	/// has different dimensions.<para/>
	/// NOTE: It is assumed that the names of both variable have already been sanitized and adjusted against naming collision in the previously
	/// declared code. If there are naming collisions, it is not this methods's fault. No code is emitted if both function names are identical
	/// and of directly compatible types.
	/// </summary>
	/// <param name="_first">The first variable whose value we wish to cast and assign to the second variable.</param>
	/// <param name="_second">The second variable whom we want to assign a new value to. Must be of a cast-compatible type.</param>
	/// <param name="_dstBuilder">A string builder instance to write the type name into. The builder is never cleared by this method.</param>
	/// <param name="_secondWasAlreadyDeclared"></param>
	/// <param name="_padding"></param>
	/// <returns></returns>
	public static bool CreateCast(ShaderGenVariable _first, ShaderGenVariable _second, StringBuilder _dstBuilder, bool _secondWasAlreadyDeclared = false, ShaderGenCastPadding _padding = ShaderGenCastPadding.ZeroOrFalse)
	{
		if (_first == null || _second == null || _dstBuilder == null) return false;

		// Double-check if a cast is actually needed here:
		bool isDirectlyCompatible = IsDirectlyCompatible(_first, _second, true);

		// Abort early
		if (isDirectlyCompatible && string.CompareOrdinal(_first.Name, _second.Name) == 0)
		{
			Logger.Instance?.LogWarning($"Possible naming collision or redefinition of shader variable '{_first}'!");
			return true;
		}

		// If second variable was not declared yet, prefix its type name:
		if (!_secondWasAlreadyDeclared)
		{
			_second.CreateTypeName(_dstBuilder);
			_dstBuilder.Append(' ');
		}

		_dstBuilder.Append(_second.Name).Append(" = ");

		// If variables do not require casting, insert an assignment instead:
		if (isDirectlyCompatible)
		{
			// Output: "float dstName = srcName"
			_dstBuilder.Append(_first.Name);
			return true;
		}

		// Write assignment code:
		return _first.GetTensorType() switch
		{
			ShaderGenTensorType.Scalar => CreateScalarCast(_first, _second, _dstBuilder),
			ShaderGenTensorType.Vector => CreateVectorCast(_first, _second, _dstBuilder, _padding),
			ShaderGenTensorType.Matrix => false,	//TODO [later]
			_ => false,
		};
	}

	private static bool CreateScalarCast(ShaderGenVariable _first, ShaderGenVariable _second, StringBuilder _dstBuilder)
	{
		if (_second.BaseType == ShaderGenBaseDataType.Bool && _first.BaseType.IsNumericType())
		{
			// Output: "bool dstName = srcName != 0"	(cast to bool)
			_dstBuilder.Append(_first.Name).Append(" != 0");
		}
		if (_first.BaseType == ShaderGenBaseDataType.Bool && _second.BaseType.IsNumericType())
		{
			// Output: "int dstName = srcName ? 1 : 0"	(cast bool to number)
			_dstBuilder.Append(_first.Name).Append(" ? 1 : 0");
		}
		else
		{
			// Output: "float dstName = (float)srcName"	(cast numeric types)
			_dstBuilder.Append('(').Append(_second.BaseType).Append(')').Append(_first.Name);
		}
		return true;
	}

	private static bool CreateVectorCast(ShaderGenVariable _first, ShaderGenVariable _second, StringBuilder _dstBuilder, ShaderGenCastPadding _padding)
	{
		// Same size, use a direct cast:
		if (_first.SizeX == _second.SizeX)
		{
			// Output: "float dstName = (float)srcName"
			_dstBuilder.Append(_second.Name).Append('(');
			_second.CreateTypeName(_dstBuilder);
			_dstBuilder.Append(')').Append(_first.Name);

			return true;
		}
		// Destination is larger, padd additional fields with 0 or 1:
		else if (_first.SizeX < _second.SizeX)
		{
			// Output: "float3 dstName = float3((float2)srcName, 0)"
			string paddingTxt = _padding == ShaderGenCastPadding.OneOrTrue ? ", 1" :  ", 0";

			_second.CreateTypeName(_dstBuilder);
			_dstBuilder.Append('(');

			// Add inner cast if source data type mismatches destination type:
			if (_first.BaseType != _second.BaseType)
			{
				_dstBuilder.Append('(');
				CreateTypeName(_second.BaseType, _first.SizeX, 1, _dstBuilder);
				_dstBuilder.Append(')');
			}

			_dstBuilder.Append(_first.Name);
			for (uint i = _first.SizeX; i < _second.SizeX; i++)
			{
				_dstBuilder.Append(paddingTxt);
			}
			_dstBuilder.Append(')');
			
			return true;
		}
		// Destination is smaller:
		else
		{
			// This case is undefined, as it may lead to unintended data omissions. Return an error instead:
			Logger.Instance?.LogWarning($"Vector type variable '{_first}' cannot be safely cast to variable '{_second}', possible unintended loss of data!");
			return false;
		}
	}

	public override string ToString()
	{
		return $"{BaseType}{SizeX}x{SizeY} {Name ?? "NULL"} ({GetTensorType()})";
	}

	#endregion
}
