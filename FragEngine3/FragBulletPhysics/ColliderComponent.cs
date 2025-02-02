using BulletSharp;
using FragEngine3.Scenes;
using System.Numerics;

namespace FragBulletPhysics;

public abstract class ColliderComponent(SceneNode _node) : Component(_node)
{
	#region Constructors

	~ColliderComponent()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Properties

	public CollisionShape CollisionShape { get; protected set; } = null!;

	public Vector3 LocalInertia { get; set; } = Vector3.Zero;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		CollisionShape?.Dispose();

		base.Dispose(_disposing);
	}

	#endregion
}
