using System.Numerics;

namespace FragEngine3.Scenes;

public sealed partial class SceneNode
{
	#region Fields

	private Pose localPose = Pose.Identity;

	#endregion
	#region Constants

	private const float DEG2RAD = MathF.PI / 180.0f;

	#endregion
	#region Properties

	public Pose LocalTransformation
	{
		get => localPose;
		set => localPose = value;
	}
	public Vector3 LocalPosition
	{
		get => localPose.position;
		set => localPose.position = value;
	}
	public Quaternion LocalRotation
	{
		get => localPose.rotation;
		set => localPose.rotation = value;
	}
	public Vector3 LocalScale
	{
		get => localPose.scale;
		set => localPose.scale = value;
	}

	public Vector3 LocalRight => Vector3.Transform(Vector3.UnitX, LocalRotation);
	public Vector3 LocalUp => Vector3.Transform(Vector3.UnitY, LocalRotation);
	public Vector3 LocalForward => Vector3.Transform(Vector3.UnitZ, LocalRotation);

	public Pose WorldTransformation
	{
		get => GetWorldPose();
		set => localPose = parentNode != null ? parentNode.TransformWorldToLocal(value) : value;
	}
	public Vector3 WorldPosition
	{
		get => GetWorldPose().position;
		set => localPose.position = parentNode != null ? parentNode.TransformWorldToLocalPoint(value) : value;
	}
	public Quaternion WorldRotation
	{
		get => GetWorldRotation();
		set => localPose.rotation = parentNode != null ? parentNode.TransformWorldToLocal(value) : value;
	}
	public Vector3 WorldScale
	{
		get => GetWorldPose().scale;
		set => localPose.scale = parentNode != null ? parentNode.TransformWorldToLocalDirection(value) : value;
	}

	public Vector3 WorldRight => Vector3.Transform(Vector3.UnitX, WorldRotation);
	public Vector3 WorldUp => Vector3.Transform(Vector3.UnitY, WorldRotation);
	public Vector3 WorldForward => Vector3.Transform(Vector3.UnitZ, WorldRotation);

	#endregion
	#region Methods

	/// <summary>
	/// Sets the orientation of this node from yaw, pitch, and roll angles.
	/// </summary>
	/// <param name="_yaw">Yaw angle (left-right), in radians or degrees.</param>
	/// <param name="_pitch">Pitch angle (up-down), in radians or degrees.</param>
	/// <param name="_roll">Roll angle (side-to-side), in radians or degrees.</param>
	/// <param name="_setWorldSpaceRotation">Whether the new rotation should be set in world space. If false, it will be set in local space instead.</param>
	/// <param name="_valuesAreDegrees">Whether the given pitch/roll/yaw angles are in degrees. If false, they must be in radians.</param>
	public void SetRotationFromYawPitchRoll(float _yaw, float _pitch, float _roll, bool _setWorldSpaceRotation, bool _valuesAreDegrees)
	{
		if (_valuesAreDegrees)
		{
			_yaw *= DEG2RAD;
			_pitch *= DEG2RAD;
			_roll *= DEG2RAD;
		}

		Quaternion newRotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, _roll);

		if (_setWorldSpaceRotation)
		{
			WorldRotation = newRotation;
		}
		else
		{
			LocalRotation = newRotation;
		}
	}

	/// <summary>
	/// Sets the orientation of this node from a rotation around an axis.
	/// </summary>
	/// <param name="_axis">A vector describing the axis of rotation. Must be a normalized vector.</param>
	/// <param name="_angle">The angle by which to rotate around the given axis, in radians or degrees.</param>
	/// <param name="_setWorldSpaceRotation">Whether the new rotation should be set in world space. If false, it will be set in local space instead.</param>
	/// <param name="_angleInDegrees">Whether the given angle is in degrees. If false, it must be in radians.</param>
	public void SetRotationFromAxisAngle(Vector3 _axis, float _angle, bool _setWorldSpaceRotation, bool _angleInDegrees)
	{
		if (_angleInDegrees)
		{
			_angle *= DEG2RAD;
		}

		Quaternion newRotation = Quaternion.CreateFromAxisAngle(_axis, _angle);

		if (_setWorldSpaceRotation)
		{
			WorldRotation = newRotation;
		}
		else
		{
			LocalRotation = newRotation;
		}
	}

	public Pose TransformWorldToLocal(Pose _worldPose)
	{
		return GetWorldPose().InverseTransform(_worldPose);
	}

	public Vector3 TransformWorldToLocalPoint(Vector3 _worldPoint)
	{
		return GetWorldPose().InverseTransformPoint(_worldPoint);
	}
	public Vector3 TransformWorldToLocalDirection(Vector3 _worldDir)
	{
		return GetWorldPose().InverseTransformDirection(_worldDir);
	}
	public Quaternion TransformWorldToLocal(Quaternion _worldRot)
	{
		return Quaternion.Conjugate(WorldRotation) * _worldRot;
	}

	public Pose TransformLocalToWorld(Pose _localPose)
	{
		return GetWorldPose().Transform(_localPose);
	}

	public Vector3 TransformLocalToWorldPoint(Vector3 _localPoint)
	{
		return GetWorldPose().TransformPoint(_localPoint);
	}
	public Vector3 TransformLocalToWorldDirection(Vector3 _localDir)
	{
		return GetWorldRotation().Rotate(_localDir);
	}
	public Quaternion TransformLocalToWorld(Quaternion _localRot)
	{
		return GetWorldRotation() * _localRot;
	}

	/// <summary>
	/// Calculates the world space transformation of this node.
	/// </summary>
	/// <returns>A pose decribing the node's transformtion in world space.</returns>
	private Pose GetWorldPose()
	{
		Pose pose = LocalTransformation;
		SceneNode? parent = parentNode;
		while (parent != null)
		{
			pose = parent.LocalTransformation.Transform(pose);
			parent = parent.parentNode;
		}
		return pose;
	}
	/// <summary>
	/// Calculates the world space rotation of this node. Scale and tranlation are ignored completely.
	/// </summary>
	/// <returns>The node's rotation in world space.</returns>
	private Quaternion GetWorldRotation()
	{
		Quaternion rot = LocalRotation;
		SceneNode? parent = parentNode;
		while (parent != null)
		{
			rot = parent.LocalRotation * rot;
			parent = parent.parentNode;
		}
		return rot;
	}

	#endregion
}
