using BulletSharp;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;

namespace FragBulletPhysics;

public sealed class PhysicsWorldComponent : Component, IOnFixedUpdateListener
{
	#region Constructors

	public PhysicsWorldComponent(SceneNode _node) : base(_node)
	{
		timeManager = _node.scene.engine.TimeManager;
		logger = _node.Logger;

		collisionConfig = new DefaultCollisionConfiguration();
		dispatcher = new CollisionDispatcher(collisionConfig);
		broadphase = new DbvtBroadphase();

		instance = new(dispatcher, broadphase, null, collisionConfig)
		{
			Gravity = gravityAcceleration,
		};
		instance.OnDispose += OnInstanceDisposed;

		
		dummyShape = new BoxShape(0.1f);
		using RigidBodyConstructionInfo dummyRigidbodyInfo = new(1, new DefaultMotionState(Matrix4x4.CreateTranslation(-100, -100, -100)), dummyShape);
		dummyRigidbody = new(dummyRigidbodyInfo);
		instance.AddCollisionObject(dummyRigidbody);
	}

	#endregion
	#region Fields

	//TEST
	//private BoxShape bs;
	//private RigidBody rb;
	//TEST

	private readonly TimeManager timeManager;
	private readonly Logger logger;

	private readonly CollisionConfiguration collisionConfig;
	private readonly Dispatcher dispatcher;
	private readonly BroadphaseInterface broadphase;

	public readonly DiscreteDynamicsWorld instance;

	private readonly CollisionShape dummyShape;
	private readonly RigidBody dummyRigidbody;

	private readonly HashSet<PhysicsBodyComponent> bodies = new(100);

	private float fixedDeltaTime = 0.01f;
	private Vector3 gravityAcceleration = new(0, -9.81f, 0);

	#endregion
	#region Properties

	public float FixedDeltaTime
	{
		get => fixedDeltaTime;
		set => fixedDeltaTime = Math.Max(value, 0.001f);
	}

	public Vector3 Gravity
	{
		get => gravityAcceleration;
		set
		{
			gravityAcceleration = value;
			if (!IsDisposed)
			{
				instance.Gravity = gravityAcceleration;
			}
		}
	}

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		if (instance is not null && !instance.IsDisposed)
		{
			instance.OnDispose -= OnInstanceDisposed;
		}

		//TEST
		dummyRigidbody.Dispose();
		dummyShape.Dispose();
		//TEST

		instance?.Dispose();
		broadphase?.Dispose();
		dispatcher?.Dispose();
		collisionConfig?.Dispose();
		
		base.Dispose(_disposing);
	}

	private void OnInstanceDisposed() => Dispose(true);

	public bool OnFixedUpdate()
	{
		if (!node.IsEnabled) return true;

		float deltaTime = (float)timeManager.DeltaTime.TotalSeconds;

		try
		{
			instance.StepSimulation(deltaTime, 5, FixedDeltaTime);
		}
		catch (Exception ex)
		{
			logger.LogException("Failed to update physics simulation!", ex);
			return false;
		}

		foreach (PhysicsBodyComponent body in bodies)
		{
			body.UpdateNodeFromPhysics();
			//if (!body.IsStatic)
			//{
			//	body.node.WorldTransformation = new(rb.WorldTransform);
			//}
		}
		return true;
	}

	internal bool RegisterBody(PhysicsBodyComponent _newBody)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot register new physics body in disposed physics world!");
			return false;
		}

		//instance.AddCollisionObject(_newBody.Rigidbody);
		instance.AddRigidBody(_newBody.Rigidbody);
		_newBody.Rigidbody.Gravity = gravityAcceleration;

		bodies.Add(_newBody);
		return true;
	}

	internal bool UnregisterBody(PhysicsBodyComponent _body)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot unregister physics body from disposed physics world!");
			return false;
		}
		if (_body is null)
		{
			logger.LogError("Cannot unregister null body from physics world!");
			return false;
		}

		instance.RemoveCollisionObject(_body.Rigidbody);
		bool removed = bodies.Remove(_body);
		return removed;
	}

	internal static bool TryFindPhysicsWorld(SceneNode _node, out PhysicsWorldComponent? _outWorldComponent)
	{
		if (_node is null || _node.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot find physics world component using disposed rigidbody component or scene node!");
			_outWorldComponent = null;
			return false;
		}

		return _node.scene.FindComponentOfType(false, out _outWorldComponent);
	}

	internal static bool TryFindOrCreatePhysicsWorld(SceneNode _node, out PhysicsWorldComponent? _outWorldComponent)
	{
		if (TryFindPhysicsWorld(_node, out _outWorldComponent))
		{
			return true;
		}

		return !_node.scene.rootNode.CreateComponent(out _outWorldComponent);
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
