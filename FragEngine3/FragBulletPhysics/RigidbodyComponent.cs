using BulletSharp;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;

namespace FragBulletPhysics;

[Obsolete("Rigidbody dynamics are included in collision shapes in Bullet")]
public sealed class RigidbodyComponent : Component, IOnComponentAddedListener, IOnComponentRemovedListener //TODO: Add Enabled/Disabled listeners, to pause or resume simulation of this body!
{
	#region Constructors

	public RigidbodyComponent(SceneNode _node, PhysicsWorldComponent? _world = null) : base(_node)
	{
		// Assign, find, or create a physics world in the component's scene:
		if (_world is null && !PhysicsWorldComponent.TryFindOrCreatePhysicsWorld(node, out _world))
		{
			throw new Exception("Could not find or create physics world for rigidbody component!");
		}
		World = _world!;

		// Find or cteate the body's collision shape:
		CollisionShape CurCollisionShape;
		Vector3 localInertia;
		if (TryFindCollider(out ColliderComponent? colliderComp))
		{
			tempCollisonShape = null;
			collider = colliderComp!;
			CurCollisionShape = collider.CollisionShape;
			localInertia = collider.LocalInertia;
		}
		else
		{
			tempCollisonShape = new SphereShape(0.1f);
			collider = null;
			CurCollisionShape = tempCollisonShape!;
			localInertia = Vector3.Zero;
		}

		// Create the underlying Bullet rigidbody instance:
		Matrix4x4 mtxInitialPose = _node.WorldTransformation.Matrix;
		DefaultMotionState initialMotionState = new(mtxInitialPose);

		using RigidBodyConstructionInfo info = new(1.0f, initialMotionState, CurCollisionShape, localInertia);

		instance = new(info);
		World.instance.AddRigidBody(instance);
	}
	
	~RigidbodyComponent()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly RigidBody instance;

	private CollisionShape? tempCollisonShape = null;
	private ColliderComponent? collider = null;

	private float mass = 1.0f;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the physics world that this body's interactions are simulated in.
	/// </summary>
	public PhysicsWorldComponent World { get; private set; }
	/// <summary>
	/// Gets the collision shape that is used for this body's collision detections.
	/// </summary>
	public CollisionShape CurrentCollisionShape => !IsDisposed ? collider?.CollisionShape ?? tempCollisonShape! : null!;

	/// <summary>
	/// Gets or sets the physical mass of this body.
	/// </summary>
	public float Mass
	{
		get => mass;
		set
		{
			mass = Math.Clamp(value, 0.01f, 100000.0f);
			Vector3 localIntertia = CurrentCollisionShape.CalculateLocalInertia(mass);
			instance.SetMassProps(mass, localIntertia);
		}
	}

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		if (_disposing && collider is not null && !collider.IsDisposed)
		{
			//collider.SetAssignedRigidbody(null);
		}

		instance.Dispose();
		tempCollisonShape?.Dispose();

		collider = null;

		base.Dispose(_disposing);
	}

	private bool AssignColliderComponent(ColliderComponent _newComponent)
	{
		instance.CollisionShape = _newComponent.CollisionShape;

		tempCollisonShape?.Dispose();
		tempCollisonShape = null;

		//_newComponent.SetAssignedRigidbody(this);
		return true;
	}

	private bool AssignTemporaryCollisionShape()
	{
		collider = null;

		tempCollisonShape = new SphereShape(0.1f);
		instance.CollisionShape = tempCollisonShape;
		return true;
	}

	private bool TryFindCollider(out ColliderComponent? _outComponent)
	{
		if (IsDisposed || node.IsDisposed)
		{
			Logger.LogError("Cannot get collider component from disposed rigidbody component or scene node!");
			_outComponent = null;
			return false;
		}

		if (!node.FindComponent(o => !o.IsDisposed && o is ColliderComponent collComp, out Component? component))
		{
			_outComponent = null;
			return false;
		}

		_outComponent = (component as ColliderComponent)!;
		return true;
	}

	public void OnComponentAdded(Component _newComponent)
	{
		// If we don't have a collider assigned, check if the new component fits the bill:
		if (collider is not null && !collider.IsDisposed)
		{
			return;
		}
		if (_newComponent is ColliderComponent newColliderComp)
		{
			AssignColliderComponent(newColliderComp);
		}
	}

	public void OnComponentRemoved(Component _removedComponent)
	{
		if (_removedComponent != collider) return;

		collider = null;

		if (!TryFindCollider(out ColliderComponent? newColliderComp) || !AssignColliderComponent(newColliderComp!))
		{
			AssignTemporaryCollisionShape();
		}
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		return true;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		_componentData = new()
		{
			SerializedData = string.Empty,
		};
		return true;
	}

	#endregion
}
