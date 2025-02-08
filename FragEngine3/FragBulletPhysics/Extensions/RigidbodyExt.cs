using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Utility;
using System.Numerics;

namespace FragBulletPhysics.Extensions;

/// <summary>
/// Helper class with extension methods for the <see cref="RigidBody"/> class.
/// </summary>
public static class RigidbodyExt
{
	#region Methods

	/// <summary>
	/// Sets the rigidbody's world transformation from a pose.<para/>
	/// Note: This method takes care of conversion between the engine's left-handed coordinate system, and Bullet's right-handed system.
	/// If your pose is already right-handed (i.e. Z-up), you should set the transformation directly via <see cref="CollisionObject.WorldTransform"/>.
	/// </summary>
	/// <param name="_rigidbody">This rigidbody instance.</param>
	/// <param name="_newPose">The new pose, in world space, using a left-handed coordinate system.</param>
	public static void SetWorldPose(this RigidBody _rigidbody, Pose _newPose)
	{
		_newPose = _newPose.ConvertHandedness();
		_rigidbody.WorldTransform = _newPose.Matrix;
	}

	/// <summary>
	/// Sets the rigidbody's world transformation from a left-handed transformation/world matrix.<para/>
	/// Note: This method takes care of conversion between the engine's left-handed coordinate system, and Bullet's right-handed system.
	/// If your matrix is already right-handed (i.e. Z-up), you should set the transformation directly via <see cref="CollisionObject.WorldTransform"/>.
	/// </summary>
	/// <param name="_rigidbody">This rigidbody instance.</param>
	/// <param name="_mtxTransformation">The new transformation, in world space, using a left-handed coordinate system.</param>
	public static void SetWorldTransform_LeftHanded(this RigidBody _rigidbody, Matrix4x4 _mtxTransformation)
	{
		_mtxTransformation = _mtxTransformation.ConvertHandedness();
		_rigidbody.WorldTransform = _mtxTransformation;
	}

	#endregion
}
