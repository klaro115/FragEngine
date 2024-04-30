using System.Numerics;

namespace FragEngine3;

/// <summary>
/// Extension methods for working with '<see cref="Quaternion"/>' rotations.
/// </summary>
public static class QuaternionExt
{
	#region Methods

	/// <summary>
	/// Rotate a vector around the coordinate origin.
	/// </summary>
	/// <param name="_rotation">This quaternion, which is expected to be a unit quaternion representing the desired rotation.</param>
	/// <param name="_vector">The vector you wish to rotate around coordinate origin.</param>
	/// <remarks>Link for reference: https://math.stackexchange.com/questions/40164/how-do-you-rotate-a-vector-by-a-unit-quaternion</remarks>
	/// <returns>The result of the rotation.</returns>
	public static Vector3 Rotate(this Quaternion _rotation, Vector3 _vector)
	{
		// Represent vector as a quaternion P:
		Quaternion p = new(_vector.X, _vector.Y, _vector.Z, 1);

		// P' = Q * P * Q^-1
		Quaternion r = (_rotation * p) * Quaternion.Conjugate(_rotation);

		// Reconstruct resulting vector:
		return new Vector3(r.X, r.Y, r.Z);
	}

	public static Quaternion CreateFromLookAt(Vector3 _forward, bool _normalizeVectors = true)			//TODO: not properly tested, has weird behaviour glitches. Might need additional vector normalizations?
	{
		if (_normalizeVectors)
		{
			_forward = Vector3.Normalize(_forward);
		}

		// Create the shortest rotation from Z unit vector and forward:
		Vector3 axis = Vector3.Cross(_forward, Vector3.UnitZ);
		float angleRad = -VectorExt.Angle(_forward, Vector3.UnitZ);

		return Quaternion.CreateFromAxisAngle(axis, angleRad);
	}

	public static Quaternion CreateFromLookAt(Vector3 _forward, Vector3 _up, bool _normalizeVectors = true)
	{
		if (_normalizeVectors)
		{
			_up = Vector3.Normalize(_up);
		}

		// Create normal lookat orientation, with 'up' pointed towards Y unit vector:
		Quaternion rotation = CreateFromLookAt(_forward, _normalizeVectors);

		// Rotate orientation along forward axis to align with 'up':
		if (_up != Vector3.UnitY)
		{
			_up = Vector3.Normalize(_up.ProjectToPlane(_forward));
			float angleRad = VectorExt.Angle(_up, rotation.Rotate(Vector3.UnitY));

			rotation *= Quaternion.CreateFromAxisAngle(_forward, angleRad);
		}

		return rotation;
	}

	#endregion
}
