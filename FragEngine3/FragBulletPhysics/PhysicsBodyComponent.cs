using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;

namespace FragBulletPhysics;

//TODO 1: Rename colliders to "PhysicsBody" or something. No need for poorly applied Unity terminology.
//TODO 2: Add Enabled/Disabled listeners, to pause or resume simulation of this body!
public abstract class PhysicsBodyComponent : Component, IOnFixedUpdateListener
{
	#region Constructors

	protected PhysicsBodyComponent(SceneNode _node, PhysicsWorldComponent _world) : base(_node)
	{
		// Assign, find, or create a physics world in the component's scene:
		PhysicsWorldComponent? tempWorld = _world;
		if (_world is null && !PhysicsWorldComponent.TryFindOrCreatePhysicsWorld(node, out tempWorld))
		{
			throw new Exception("Could not find or create physics world for rigidbody component!");
		}
		World = tempWorld!;

		if (!ReinitializeBody())
		{
			throw new Exception("Failed to initialize collider component!");
		}
	}

	~PhysicsBodyComponent()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private bool isStatic = true;
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
		protected set
		{
			collisionShape = value;
			rigidbody.CollisionShape = collisionShape;

			UpdateMass();
		}
	}

	/// <summary>
	/// Gets the instance controlling this body's rigidbody dynamics. Null if the body is static.
	/// </summary>
	public RigidBody Rigidbody
	{
		get => rigidbody;
		private set => rigidbody = value;
	}

	/// <summary>
	/// Gets or sets whether this body is static. Static objects will take part in collisions, but act as immovable walls.
	/// </summary>
	public bool IsStatic
	{
		get => isStatic;
		set
		{
			if (value == isStatic) return;

			isStatic = value;
			UpdateMass();
		}
	}

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
			UpdateMass();
		}
	}

	protected float ActualMass => isStatic ? 0 : dynamicMass;

	/// <summary>
	/// Gets the local inertia vector of this body.
	/// </summary>
	public Vector3 LocalInertia { get; protected set; } = Vector3.Zero;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		rigidbody?.Dispose();
		collisionShape?.Dispose();

		base.Dispose(_disposing);
	}

	protected bool ReinitializeBody(bool _forceRecreate = false)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot reinitialize disposed physics body!");
			return false;
		}

		// Unregister previous rigidbody instance:
		if (rigidbody is not null)
		{
			World.UnregisterBody(this);
		}

		// Create collision shape:
		collisionShape?.Dispose();

		if (!InitializeCollisionShape(out collisionShape))
		{
			Logger.LogError("Failed to (re)initialize collision shape of physics body!");
			return false;
		}
		UpdateLocalInertia();

		// Create rigidbody:
		Matrix4x4 mtxWorldPose = node.WorldTransformation.Matrix;

		if (_forceRecreate || rigidbody is null || rigidbody.IsDisposed)
		{
			if (_forceRecreate) rigidbody?.Dispose();

			try
			{
				using RigidBodyConstructionInfo rigidbodyInfo = new(
					ActualMass,
					new DefaultMotionState(mtxWorldPose),
					collisionShape);

				rigidbody = new(rigidbodyInfo);
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to initialize rigidbody instance of physics body!", ex);
				return false;
			}
		}
		else
		{
			rigidbody.WorldTransform = mtxWorldPose;
			rigidbody.CollisionShape = collisionShape;
		}

		// Register body in the physics world:
		bool success = World.RegisterBody(this);
		return success;
	}

	/// <summary>
	/// Try to create, update, or recreate the body's collision shape.
	/// </summary>
	/// <param name="_outCollisionShape">Outputs the collision shape to use with this body.</param>
	/// <returns>True if the collision shape was initialized, false otherwise.</returns>
	protected abstract bool InitializeCollisionShape(out CollisionShape _outCollisionShape);

	protected void UpdateMass()
	{
		float actualMass;
		if (isStatic)
		{
			actualMass = 0; // In the Bullet physics engine, objects are static if their mass is 0.
			LocalInertia = Vector3.Zero;
		}
		else
		{
			actualMass = dynamicMass;
			LocalInertia = collisionShape.CalculateLocalInertia(dynamicMass);
		}

		rigidbody.SetMassProps(actualMass, LocalInertia);
	}

	protected void UpdateLocalInertia()
	{
		if (isStatic)
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
		if (isStatic) return;

		//node.WorldPosition = rigidbody.WorldTransform.Translation;
		node.WorldTransformation = new(rigidbody.WorldTransform.Translation);
	}

	public bool OnFixedUpdate()
	{
		if (!isStatic)
		{		
			Console.WriteLine($"Sphere: Mass={ActualMass}, Position={rigidbody.WorldTransform.Translation}, Velocity={rigidbody.LinearVelocity}, Gravity={rigidbody.Gravity}");
		}
		return true;
	}

	#endregion
}
