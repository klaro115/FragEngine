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

		try
		{
			collisionConfig = new();
			dispatcher = new(collisionConfig);
			broadphase = new();
			BroadphaseInterface b = new AxisSweep3(Vector3.One * -100, Vector3.One * 100);
		}
		catch (Exception ex)
		{
			logger.LogException("Failed to create dependencies for physics world!", ex);
			Dispose();
			return;
		}

		instance = new(dispatcher, broadphase, null, collisionConfig)
		{
			Gravity = gravityAcceleration
		};
	}

	#endregion
	#region Fields

	private readonly TimeManager timeManager;
	private readonly Logger logger;

	private readonly DefaultCollisionConfiguration collisionConfig = null!;
	private readonly CollisionDispatcher dispatcher = null!;
	private readonly DbvtBroadphase broadphase = null!;

	public readonly DiscreteDynamicsWorld instance = null!;

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
		instance?.Dispose();
		broadphase?.Dispose();
		dispatcher?.Dispose();
		collisionConfig?.Dispose();
		
		base.Dispose(_disposing);
	}

	public bool OnFixedUpdate()
	{
		if (!node.IsEnabled) return true;

		float deltaTime = (float)timeManager.DeltaTime.TotalSeconds;

		try
		{
			instance.StepSimulation(deltaTime, 5, FixedDeltaTime);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogException("Failed to update physics simulation!", ex);
			return false;
		}
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
