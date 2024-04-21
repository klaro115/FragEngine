using System.Numerics;

namespace FragEngine3;

public static class VectorExt
{
	#region Constants

	private const float RAD2DEG = 180.0f / MathF.PI;

	#endregion
	#region Methods

	/// <summary>
	/// Calculate the angle between two vectors, in radians.
	/// </summary>
	/// <param name="_u">The first vector.</param>
	/// <param name="_v">The second vector.</param>
	/// <param name="_normalizeVectors">Whether to first normalize vectors. Set this to true if the vectors are not of unit length.</param>
	/// <returns>An angle measurement in radians.</returns>
	public static float Angle(Vector3 _u, Vector3 _v, bool _normalizeVectors = true)
	{
		// 
		if (_normalizeVectors)
		{
			_u = Vector3.Normalize(_u);
			_v = Vector3.Normalize(_v);
		}

		// Dot product can be calculated as:
		// dot(u, v) = u * v = |u| * |v| * cos(a)
		// cos(a) = dot(u, v) / (|u| * |v|)

		// u and v are normalized, so their lengths are 1:
		// cos(a) = dot(u, v) / 1
		// a = acos(dot(u, v))

		return MathF.Acos(Vector3.Dot(_u, _v));
	}

	/// <summary>
	/// Calculate the angle between two vectors, in degrees.
	/// </summary>
	/// <param name="_u">The first vector.</param>
	/// <param name="_v">The second vector.</param>
	/// <param name="_normalizeVectors">Whether to first normalize vectors. Set this to true if the vectors are not of unit length.</param>
	/// <returns>An angle measurement in degrees.</returns>
	public static float AngleDeg(Vector3 _u, Vector3 _v, bool _normalizeVectors = true) => Angle(_u, _v, _normalizeVectors) * RAD2DEG;

	/// <summary>
	/// Project the vector onto a plane.
	/// </summary>
	/// <param name="_vector">This vector, denoting some direction or position.</param>
	/// <param name="_planeNormal">The normal vector of the plane. The plane is assumed to be crossing the coordinate origin.</param>
	/// <remarks>Link for reference: https://www.maplesoft.com/support/help/maple/view.aspx?path=MathApps%2FProjectionOfVectorOntoPlane</remarks>
	/// <returns></returns>
	public static Vector3 ProjectToPlane(this Vector3 _vector, Vector3 _planeNormal)
	{
		// Project vector onto the normal vector:
		Vector3 vProj = Vector3.Dot(_vector, _planeNormal) / _planeNormal.LengthSquared() * _vector;

		// Subtract normal component from original vector to retain only planar components:
		return _vector - vProj;
	}

	public static float MoveTowards(float _from, float _to, float _maxChange)
	{
		float diff = _to - _from;
		if (Math.Abs(diff) <= _maxChange)
		{
			return _to;
		}
		return _from + Math.Sign(diff) * _maxChange;
        }

	public static Vector3 MoveTowards(Vector3 _from, Vector3 _to, float _maxChange)
	{
		return new Vector3(
			MoveTowards(_from.X, _to.X, _maxChange),
			MoveTowards(_from.Y, _to.Y, _maxChange),
			MoveTowards(_from.Z, _to.Z, _maxChange));
	}

	#endregion
}
