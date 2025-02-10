using BulletSharp;
using FragEngine3;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;

namespace FragBulletPhysics;

public sealed class PhysicsWorldComponent : Component, IOnFixedUpdateListener
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(PhysicsWorldComponent))]
	public sealed class Data
	{
		public float FixedDeltaTime {  get; set; }
		public Vector3 Gravity {  get; set; }
	}

	#endregion
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
	}

	#endregion
	#region Fields

	private readonly TimeManager timeManager;
	private readonly Logger logger;

	private readonly CollisionConfiguration collisionConfig;
	private readonly Dispatcher dispatcher;
	private readonly BroadphaseInterface broadphase;

	public readonly DiscreteDynamicsWorld instance;

	private readonly HashSet<PhysicsBodyComponent> bodies = new(100);

	private float fixedDeltaTime = 0.01f;
	private Vector3 gravityAcceleration = new(0, -9.81f, 0);

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets the desired interval between discrete physics simulation steps, in seconds.
	/// </summary>
	public float FixedDeltaTime
	{
		get => fixedDeltaTime;
		set => fixedDeltaTime = Math.Max(value, 0.001f);
	}

	/// <summary>
	/// Gets or sets the direction and intensity of gravitational acceleration, in world units per second squared.
	/// </summary>
	public Vector3 Gravity
	{
		get => gravityAcceleration;
		set
		{
			gravityAcceleration = value;
			if (!IsDisposed)
			{
				instance.Gravity = gravityAcceleration.ConvertHandedness();
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
		}
		return true;
	}

	/// <summary>
	/// Adds a new physics body to this world's simulation.
	/// </summary>
	/// <param name="_newBody">The new body we wish to add to this world.
	/// Each body can only be registered once; additional calls to register it will be ignored.</param>
	/// <returns>True if the body was added to the simulation, false on error.</returns>
	internal bool RegisterBody(PhysicsBodyComponent _newBody)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot register new physics body in disposed physics world!");
			return false;
		}

		if (!bodies.Contains(_newBody))
		{
			instance.AddRigidBody(_newBody.Rigidbody);
			bodies.Add(_newBody);
		}
		return true;
	}

	/// <summary>
	/// Removes a physics body from this world's simulation.
	/// </summary>
	/// <param name="_body">The existing body we wish to remove from this world.</param>
	/// <returns>True if the body was removed, false on error or if the body wasn't part of this world.</returns>
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

	/// <summary>
	/// Tries to find an instance of <see cref="PhysicsWorldComponent"/> in the scene.
	/// </summary>
	/// <param name="_node">A node in the scene where we are looking for a physics world.</param>
	/// <param name="_outWorldComponent">Outputs the physics world component if it was found. Null on failure.</param>
	/// <returns>True if a component was found, false otherwise.</returns>
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

	/// <summary>
	/// Tries to find an instance of <see cref="PhysicsWorldComponent"/> in the scene, and creates one on the scene's root node if none was found.
	/// </summary>
	/// <param name="_node">A node in the scene where we are looking for a physics world.</param>
	/// <param name="_outWorldComponent">Outputs the physics world component if it was found or created. Null on failure.</param>
	/// <returns>True if a component was found or created, false otherwise.</returns>
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
		if (!FragEngine3.Utility.Serialization.Serializer.DeserializeFromJson(_componentData.SerializedData, out Data? data))
		{
			return false;
		}

		FixedDeltaTime = data!.FixedDeltaTime;
		Gravity = data.Gravity;
		return true;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		Data data = new()
		{
			FixedDeltaTime = FixedDeltaTime,
			Gravity = Gravity,
		};

		if (!FragEngine3.Utility.Serialization.Serializer.SerializeToJson(data, out string jsonTxt))
		{
			_componentData = new ComponentData();
			return false;
		}

		_componentData = new ComponentData()
		{
			SerializedData = jsonTxt,
		};
		return true;
	}

	#endregion
}
