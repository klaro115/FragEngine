using System.Numerics;

namespace FragEngine3.Utility;

/// <summary>
/// Helper class with extension methods for working for matrix types, such as <see cref="Matrix4x4"/>.
/// </summary>
public static class MatrixExt
{
	#region Fields

	private static readonly Matrix4x4 mtxConvertHandedness = new(
		1, 0, 0, 0,
		0, 0, 1, 0,
		0, 1, 0, 0,
		0, 0, 0, 1);

	#endregion
	#region Methods

	/// <summary>
	/// Gets a matrix that can be used to convert between left-handed and right-handed coordinate systems.
	/// </summary>
	/// <returns>The conversion matrix.</returns>
	public static Matrix4x4 GetHandednessConversionMatrix() => mtxConvertHandedness;

	/// <summary>
	/// Applies a conversion between left-handed and right-handed coordinate systems.<para/>
	/// Note: This assumes that the left-handed coordinate system is Y-up, and that the right-handed system is Z-up,
	/// but that both are X-right. Conversion is done by swapping Y and Z axes via matrix multiplication.
	/// </summary>
	/// <param name="_mtxTransformation">The transformation matrix that you wish to convert to a different handedness.</param>
	/// <returns>A matrix with the same transformation, but in the other coordinate system.</returns>
	public static Matrix4x4 ConvertHandedness(this Matrix4x4 _mtxTransformation)
	{
		return _mtxTransformation * mtxConvertHandedness;
	}

	#endregion
}
