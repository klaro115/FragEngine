using BulletSharp;
using FragBulletPhysics.ShapeComponents;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;

namespace FragBulletPhysics;

/// <summary>
/// Base class for rigidbody physics components.<para/>
/// Note: This ain't Unity; there are no separate collider and rigidbody components. Use <see cref="IsStatic"/> to mark physics bodies as static or dynamic.
/// </summary>
public abstract class PhysicsBodyComponent : Component, IOnNodeSetEnabledListener
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(PhysicsBodyComponent))]
	public abstract class BaseData
	{
		public bool IsStatic { get; set; }
		public required float Mass { get; set; }
	}

	#endregion
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
		isStatic = _isStatic;
	}

	~PhysicsBodyComponent()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	protected bool isStatic = true;
	private float dynamicMass = 1.0f;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the physics world that this body is assigned to.
	/// </summary>
	protected PhysicsWorldComponent World { get; } = null!;

	/// <summary>
	/// Gets the instance governing the shape and collision properties of this body.
	/// </summary>
	public CollisionShape CollisionShape { get; protected set; } = null!;

	/// <summary>
	/// Gets the instance controlling this body's rigidbody dynamics. Null if the body is static.
	/// </summary>
	public RigidBody Rigidbody { get; protected set; } = null!;

	/// <summary>
	/// Gets or sets whether this body is static. Static objects will take part in collisions, but act as immovable walls.
	/// </summary>
	public virtual bool IsStatic
	{
		get => isStatic;
		set
		{
			bool wasStatic = isStatic;
			isStatic = value;
			if (isStatic != wasStatic)
			{
				UpdateMass();
			}
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
			if (float.IsNaN(value) || value <= 0) return;

			float prevMass = dynamicMass;
			dynamicMass = value;
			if (dynamicMass != prevMass)
			{
				UpdateMass();
			}
		}
	}

	/// <summary>
	/// Gets the actual internal mass of the body. If the body is static, this will return 0, if it's dynamic, the dynamic mass will be returned.
	/// </summary>
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
		if (_disposing && !World.IsDisposed)
		{
			World.UnregisterBody(this);
		}

		Rigidbody?.Dispose();
		CollisionShape?.Dispose();

		base.Dispose(_disposing);
	}

	/// <summary>
	/// Update mass and inertia properties of the rigidbody.
	/// This should be called after the mass of the body, or the dimensions of the collision shape have changed after creation.
	/// </summary>
	protected void UpdateMass()
	{
		if (isStatic)
		{
			LocalInertia = Vector3.Zero;
		}
		else
		{
			LocalInertia = CollisionShape.CalculateLocalInertia(dynamicMass);
		}
		Rigidbody.SetMassProps(ActualMass, LocalInertia);
	}

	/// <summary>
	/// Internal notification that the body should update the transformation of its host node from the rigidbody.
	/// </summary>
	internal void UpdateNodeFromPhysics()
	{
		if (IsStatic) return;

		Pose newWorldPose = new Pose(Rigidbody.WorldTransform).ConvertHandedness();
		node.WorldTransformation = newWorldPose;
	}

	public virtual void OnNodeEnabled(bool _isEnabled)
	{
		if (IsDisposed) return;

		if (_isEnabled)
		{
			World.RegisterBody(this);
		}
		else
		{
			World.UnregisterBody(this);
		}
	}

	#endregion
}
