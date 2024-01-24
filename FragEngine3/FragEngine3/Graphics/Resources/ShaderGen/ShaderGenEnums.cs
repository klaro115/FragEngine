namespace FragEngine3.Graphics.Resources.ShaderGen
{
	/// <summary>
	/// Various data types for variables and parameters that are supported in shader code.
	/// </summary>
	public enum ShaderGenBaseDataType
	{
		None		= 0,

		Half,
		Float,
		Double,
		Int,
		UInt,
		Bool,
	}

	/// <summary>
	/// Descriptive labels for variables and parameters. Variables can be scalars, vectors, or matrices.
	/// </summary>
	public enum ShaderGenTensorType
	{
		Scalar,
		Vector,
		Matrix,
	}

	/// <summary>
	/// Padding values when filling unmapped/unassigned fields within a vector or matrix.
	/// </summary>
	public enum ShaderGenCastPadding
	{
		ZeroOrFalse,
		OneOrTrue,
	}

	/// <summary>
	/// Different source and usage types for a feature's variables.
	/// </summary>
	public enum ShaderGenVariableType
	{
		None,

		Input,
		Output,
		Internal,
	}

	/// <summary>
	/// Different shader languages for which different formatting might exist.
	/// </summary>
	public enum ShaderGenLanguage
	{
		HLSL,
		Metal,
		GLSL,
	}

	public enum ShaderGenInputBasicPS
	{
		FragmentPosition,
		WorldPosition,
		Normal,
		UV,
	}
	public enum ShaderGenInputExtPS
	{
		Tangent,
		Binormal,
		UV2,
	}

	public static class ShaderGenDataTypeExt
	{
		#region Fields

		private static readonly string[] dataTypeBaseNames =
		[
			"ERROR",

			"half",
			"float",
			"double",
			"int",
			"uint",
			"bool",
		];

		public static readonly bool[] dataTypeIsNumeric =
		[
			false,

			true,
			true,
			true,
			true,
			true,
			false,
		];

		#endregion
		#region Methods

		/// <summary>
		/// Gets the name of this data type as it might appear in shader code.
		/// </summary>
		/// <param name="_dataType">This base data type.</param>
		/// <returns>A string containing the type's name.</returns>
		public static string GetBaseName(this ShaderGenBaseDataType _dataType)
		{
			int idx = (int)_dataType;
			return idx >= 0 && idx < dataTypeBaseNames.Length ? dataTypeBaseNames[idx] : string.Empty;
		}

		/// <summary>
		/// Gets whether this data type is a numeric type, aka whether it represents a number. If not, it is most likely a boolean.
		/// </summary>
		/// <param name="_dataType">This base data type.</param>
		/// <returns>True if the type represents a number, false otherwise.</returns>
		public static bool IsNumericType(this ShaderGenBaseDataType _dataType)
		{
			int idx = (int)_dataType;
			return idx >= 0 && idx < dataTypeIsNumeric.Length && dataTypeIsNumeric[idx];
		}

		#endregion
	}
}
