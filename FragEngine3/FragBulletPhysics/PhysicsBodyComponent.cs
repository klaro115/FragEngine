using BulletSharp;
using FragBulletPhysics.ShapeComponents;
using FragEngine3.Scenes;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;

namespace FragBulletPhysics;

//TODO: Add Enabled/Disabled listeners, to pause or resume simulation of this body!
public abstract class PhysicsBodyComponent : Component//, IOnFixedUpdateListener
{
	#region Constructors

	protected PhysicsBodyComponent(SceneNode _node, PhysicsWorldComponent _world, float _mass, bool _isStatic) : base(_node)
	{
		// Assign, find, or create a physics world in the component's scene:
		PhysicsWorldComponent? tempWorld = _world;
		if (_world is null && !PhysicsWorldComponent.TryFindOrCreatePhysicsWorld(node, out tempWorld))
		{
			throw new Exception("Could not find or create physics world for rigidbody component!");
		}
		World = tempWorld!;

		dynamicMass = _mass;
		IsStatic = _isStatic;
	}

	~PhysicsBodyComponent()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private float dynamicMass = 1.0f;

	private CollisionShape collisionShape = null!;
	private RigidBody rigidbody = null!;

	#endregion
	#region Properties

	protected PhysicsWorldComponent World { get; init; } = null!;

	/// <summary>
	/// Gets the instance governing the shape and collision properties of this body.
	/// </summary>
	public CollisionShape CollisionShape
	{
		get => collisionShape;
		protected set => collisionShape = value;
	}

	/// <summary>
	/// Gets the instance controlling this body's rigidbody dynamics. Null if the body is static.
	/// </summary>
	public RigidBody Rigidbody
	{
		get => rigidbody;
		protected set => rigidbody = value;
	}

	/// <summary>
	/// Gets or sets whether this body is static. Static objects will take part in collisions, but act as immovable walls.
	/// </summary>
	public bool IsStatic { get; set; } = true;

	/// <summary>
	/// Gets or sets the mass of the body.<para/>
	/// Changes will only take effect when <see cref="IsStatic"/> is true.
	/// </summary>
	public float Mass
	{
		get => dynamicMass;
		set
		{
			if (float.IsNaN(value) || value <= 0 || value == dynamicMass) return;

			dynamicMass = value;
			//UpdateMass();
		}
	}

	protected float ActualMass => IsStatic ? 0 : dynamicMass;

	/// <summary>
	/// Gets the local inertia vector of this body.
	/// </summary>
	public Vector3 LocalInertia { get; protected set; } = Vector3.Zero;

	/// <summary>
	/// Gets the shape type of this body's collision shape.
	/// </summary>
	public abstract PhysicsBodyShapeType ShapeType { get; }

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		rigidbody?.Dispose();
		collisionShape?.Dispose();

		base.Dispose(_disposing);
	}

	protected void UpdateLocalInertia()
	{
		if (IsStatic)
		{
			LocalInertia = Vector3.Zero;
		}
		else
		{
			LocalInertia = collisionShape.CalculateLocalInertia(dynamicMass);
		}
	}

	internal void UpdateNodeFromPhysics()
	{
		if (IsStatic) return;

		node.WorldTransformation = new(rigidbody.WorldTransform.Translation);
	}

	#endregion
}
