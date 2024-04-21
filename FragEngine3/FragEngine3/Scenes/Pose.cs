using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FragEngine3.Scenes;

/// <summary>
/// Structure representing an object's spatial transformations, or a coordinate space.<para/>
/// NOTE: All transformations using this type are done in the pose's parent or local space.
/// A pose is agnostic to any transformations it might represent in a scene hierarchy.
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
public struct Pose : IEquatable<Pose>
{
	#region Constructors

	public Pose()
	{
		position = Vector3.Zero;
		rotation = Quaternion.Identity;
		scale = Vector3.One;
	}
	public Pose(Vector3 _position)
	{
		position = _position;
		rotation = Quaternion.Identity;
		scale = Vector3.One;
	}
	public Pose(Vector3 _position, Quaternion _rotation, Vector3 _scale, bool _normalizeRotation = false)
	{
		position = _position;
		rotation = _normalizeRotation ? Quaternion.Normalize(_rotation) : _rotation;
		scale = _scale;
	}
	public Pose(in Matrix4x4 _mtxTransformation)
	{
		Matrix4x4.Decompose(_mtxTransformation, out scale, out rotation, out position);
	}

	#endregion
	#region Fields

	public Vector3 position;
	public Quaternion rotation;
	public Vector3 scale;

	#endregion
	#region Constants

	/// <summary>
	/// The size of this structure in bytes.
	/// </summary>
	public const int byteSize = 3 * sizeof(float) + 4 * sizeof(float) + 3 * sizeof(float);  // 10 bytes

	private const float EPSILON = 1.0e-5f;

	#endregion
	#region Properties

	/// <summary>
	/// The progressive direction along the local X axis in parent space, aka 'Right'.
	/// </summary>
	public readonly Vector3 Right => rotation.Rotate(Vector3.UnitX);
	/// <summary>
	/// The progressive direction along the local Y axis in parent space, aka 'Up'.
	/// </summary>
	public readonly Vector3 Up => rotation.Rotate(Vector3.UnitY);
	/// <summary>
	/// The progressive direction along the local Z axis in parent space, aka 'Forward'.
	/// </summary>
	public readonly Vector3 Forward => rotation.Rotate(Vector3.UnitZ);

	/// <summary>
	/// Gets or sets the values of this pose as a transformation matrix.
	/// </summary>
	public Matrix4x4 Matrix
	{
		readonly get => Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
		set => Matrix4x4.Decompose(value, out scale, out rotation, out position);
	}

	/// <summary>
	/// The pose equivalent to an identity matrix, with no translation, no rotation, and a scale of 1.
	/// </summary>
	public static Pose Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);

	#endregion
	#region Methods Transformations

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Translate(Vector3 _offset) => position += _offset;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Rotate(Quaternion _rotation) => rotation *= _rotation;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Scale(Vector3 _scale) => scale *= _scale;

	/// <summary>
	/// Rotate this pose around a point in space.
	/// </summary>
	/// <param name="_centerOfRotation">The coordinates of the center of rotation, in parent space.</param>
	/// <param name="_rotation">The rotation around that point.</param>
	public void RotateAround(Vector3 _centerOfRotation, Quaternion _rotation)
	{
		position -= _centerOfRotation;
		Rotate(_rotation);
		position += _centerOfRotation;
	}

	/// <summary>
	/// Create a pose whose forward direction is looking at a specific point in space.
	/// </summary>
	/// <param name="_position">The point from where we're looking.</param>
	/// <param name="_targetPoint">The target point we're looking at.</param>
	/// <returns>An oriented pose centered on the given position, and with a scale of 1.</returns>
	public static Pose CreateLookAt(Vector3 _position, Vector3 _targetPoint)
	{
		Vector3 forward = Vector3.Normalize(_targetPoint - _position);
		Quaternion rotation = forward.LengthSquared() > EPSILON
			? QuaternionExt.CreateFromLookAt(forward, true)
			: Quaternion.Identity;

		return new(_position, rotation, Vector3.One);
	}

	/// <summary>
	/// Create a pose whose forward direction is looking at a specific point in space.
	/// </summary>
	/// <param name="_position">The point from where we're looking.</param>
	/// <param name="_targetPoint">The target point we're looking at.</param>
	/// <param name="_up">An 'up' direction towards which to align the pose's vertical direction.</param>
	/// <returns>An oriented pose centered on the given position, and with a scale of 1.</returns>
	public static Pose CreateLookAt(Vector3 _position, Vector3 _targetPoint, Vector3 _up)
	{
		Vector3 forward = Vector3.Normalize(_targetPoint - _position);
		Quaternion rotation = forward.LengthSquared() > EPSILON
			? QuaternionExt.CreateFromLookAt(forward, _up, true)
			: Quaternion.Identity;

		return new(_position, rotation, Vector3.One);
	}

	#endregion
	#region Methods Conversions

	// LOCAL => WORLD:

	/// <summary>
	/// Transforms other pose from this pose's local space to its parent space.
	/// </summary>
	/// <param name="_localPose">The other pose, assumed to be in this pose's local space.</param>
	/// <returns>A pose in parent space.</returns>
	public readonly Pose Transform(Pose _localPose)
	{
		return new Pose(
			TransformPoint(_localPose.position),
			TransformRotation(_localPose.rotation),
			scale * _localPose.scale
		);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector3 TransformPoint(Vector3 _localPoint) => position + rotation.Rotate(scale * _localPoint);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector3 TransformDirection(Vector3 _localDir) => rotation.Rotate(_localDir);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Quaternion TransformRotation(Quaternion _localRotation) => rotation * _localRotation;

	// WORLD => LOCAL:

	private readonly Vector3 DivideByScale(Vector3 _other) => _other / Vector3.Max(scale, new Vector3(EPSILON, EPSILON, EPSILON));

	/// <summary>
	/// Transforms other pose from this pose's parent space to its local space.
	/// </summary>
	/// <param name="_worldPose">The other pose, assumed to be in this pose's parent space.</param>
	/// <returns>A pose in local space.</returns>
	public readonly Pose InverseTransform(Pose _worldPose)
	{
		Vector3 invScale = DivideByScale(Vector3.One);
		return new Pose(
			InverseTransformPoint(_worldPose.position),
			InverseTransformRotation(_worldPose.rotation),
			_worldPose.scale * invScale
		);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector3 InverseTransformPoint(Vector3 _worldPoint) => DivideByScale(Quaternion.Conjugate(rotation).Rotate(_worldPoint - position));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector3 InverseTransformDirection(Vector3 _worldDirection) => Quaternion.Conjugate(rotation).Rotate(_worldDirection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Quaternion InverseTransformRotation(Quaternion _worldRotation) => Quaternion.Conjugate(rotation) * _worldRotation;

	#region Methods Misc
	#endregion

	public override readonly bool Equals(object? _other) => _other is Pose pose && Equals(pose);
	public readonly bool Equals(Pose _other) => position == _other.position && rotation == _other.rotation && scale == _other.scale;
	public override readonly int GetHashCode() => base.GetHashCode();

	public static bool operator ==(Pose _a, Pose _b) => _a.Equals(_b);
	public static bool operator !=(Pose _a, Pose _b) => !_a.Equals(_b);

	public static implicit operator Matrix4x4(Pose _pose) => _pose.Matrix;
	public static explicit operator Pose(Matrix4x4 _mtxTransformation) => new(in _mtxTransformation);

	public override readonly string ToString()
	{
		return $"Position: ({position.X:0.0}, {position.Y:0.0}, {position.Z:0.0}), Rotation: ({rotation.X:0.0}, {rotation.Y:0.0}, {rotation.Z:0.0}, {rotation.W:0.0}), Scale: ({scale.X:0.0}, {scale.Y:0.0}, {scale.Z:0.0})";
	}

	#endregion
}
