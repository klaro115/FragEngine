using BulletSharp;
using FragEngine3.Scenes;
using System.Numerics;

namespace FragBulletPhysics;

public abstract class ColliderComponent : Component //TODO: Add Enabled/Disabled listeners, to pause or resume simulation of this body!
{
	#region Constructors

	public ColliderComponent(SceneNode _node, PhysicsWorldComponent? _world = null) : base(_node)
	{
		// Assign, find, or create a physics world in the component's scene:
		if (_world is null && !PhysicsWorldComponent.TryFindOrCreatePhysicsWorld(node, out _world))
		{
			throw new Exception("Could not find or create physics world for rigidbody component!");
		}
		World = _world!;
	}

	~ColliderComponent()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private CollisionShape collisionShape = null!;
	private CollisionObject? staticCollisionObject = null;

	#endregion
	#region Properties

	protected PhysicsWorldComponent World { get; init; } = null!;

	public CollisionShape CollisionShape
	{
		get => collisionShape;
		protected set
		{
			collisionShape = value;
			InitializeComponent();
		}
	}

	public Vector3 LocalInertia { get; set; } = Vector3.Zero;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		CollisionShape?.Dispose();

		base.Dispose(_disposing);
	}

	protected void InitializeComponent()
	{
		if (node.GetComponent<RigidbodyComponent>() is not null)
		{
			return;
		}
		if (staticCollisionObject is not null && !staticCollisionObject.IsDisposed)
		{
			return;
		}

		Matrix4x4 mtxWorldPose = node.WorldTransformation.Matrix;

		staticCollisionObject = new()
		{
			WorldTransform = mtxWorldPose,
		};
		World.instance.AddCollisionObject(staticCollisionObject);
	}

	internal void SetAssignedRigidbody(RigidbodyComponent? _component)
	{
		if (_component is null)
		{
			InitializeComponent();
		}
		else if (staticCollisionObject is not null)
		{
			World.instance.RemoveCollisionObject(staticCollisionObject);
			staticCollisionObject?.Dispose();
			staticCollisionObject = null;
		}
	}

	#endregion
}
