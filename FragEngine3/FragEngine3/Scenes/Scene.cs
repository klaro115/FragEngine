using FragEngine3.EngineCore;
using FragEngine3.Graphics.Stack;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Scenes.SceneManagers;
using FragEngine3.Scenes.SpatialTrees;

namespace FragEngine3.Scenes;

public sealed class Scene : IDisposable
{
	#region Constructors

	public Scene(Engine _engine, string? _name = null)
	{
		engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		logger = engine.Logger;
		rootNode = new SceneNode(this)
		{
			LocalTransformation = Pose.Identity
		};
		if (_name != null) Name = _name;

		updateManager = new(this);
		drawManager = new(this);
	}
	~Scene()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private string name = "Scene";

	public readonly Engine engine;
	private readonly Logger logger;
	public readonly SceneNode rootNode;
	public readonly SceneSettings settings = new();

	private readonly List<SceneBehaviour> sceneBehaviours = [];
	private IGraphicsStack? graphicsStack = null;
	private ISpatialTree? spatialPartitioning = null;

	internal readonly SceneUpdateManager updateManager;
	internal readonly SceneDrawManager drawManager;

	private EngineState updatedInEngineStates = EngineState.Running;
	private EngineState drawnInEngineStates = EngineState.Running;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	/// <summary>
	/// Gets or sets the name of this scene, must be non-null.
	/// </summary>
	public string Name
	{
		get => name;
		set => name = value ?? string.Empty;
	}

	/// <summary>
	/// Gets or sets the graphics stack to use for drawing this scene. Must be non-null. When assigning a new stack, the previous one is disposed.<para/>
	/// NOTE: If no stack is assigned when a call to '<see cref="DrawScene"/>' arrives, a default forward+light graphics stack without UI or post-processing pass
	/// is created instead.
	/// </summary>
	public IGraphicsStack? GraphicsStack
	{
		get => graphicsStack is not null && !graphicsStack.IsDisposed ? graphicsStack : null;
		set
		{
			if (value is not null && value.IsDisposed)
			{
				logger.LogError("Scene graphics stack may not be disposed!");
				return;
			}
			if (graphicsStack != null && !graphicsStack.IsDisposed)
			{
				graphicsStack.Dispose();
			}
			graphicsStack = value;
		}
	}

	/// <summary>
	/// Gets or sets the spatial partitioning structure used to accelerate culling lookups when rendering scene objects.<para/>
	/// NOTE: If no spatial partitioning is assigned when a call to '<see cref="DrawScene"/>' arrives, a default BSP tree is created instead.
	/// </summary>
	public ISpatialTree? SpatialPartitioning
	{
		get => spatialPartitioning;
		set
		{
			spatialPartitioning?.Clear();
			spatialPartitioning = value ?? CreateFallbackSpatialPartitioningTree(1);
			spatialPartitioning.Clear();
		}
	}

	/// <summary>
	/// Gets or sets flags for engine states during which this scene's logic will be updated and graphics rendered. If flags are 0, the scene will be skipped.<para/>
	/// NOTE: The only engine states that will update scene logic are 'Loading', 'Running', and 'Unloading'. Only during running state should actual game logic happen;
	/// loading/unloading state updates should be strictly reserved for loading screens.<para/>
	/// HINT: A nice use-case of resetting all flags to 0 is to disable a scene, for example to load and populate its contents in the background, whilst another
	/// scene is still active and being updated.
	/// </summary>
	public EngineState UpdatedInEngineStates
	{
		get => updatedInEngineStates;
		set
		{
			if (value.HasFlag(EngineState.Startup) || value.HasFlag(EngineState.Shutdown))
			{
				logger.LogError("Only update flags for Loading, Unloading, and Running engine states are allowed! Resetting corresponding flags.");
				value &= ~(EngineState.Startup | EngineState.Shutdown);
			}
			if (updatedInEngineStates != 0 && value == 0) logger.LogMessage($"Disabling updates of scene '{name}'...");
			else if (updatedInEngineStates == 0 && value != 0) logger.LogMessage($"Enabling updates of scene '{name}'...");
			updatedInEngineStates = value;
		}
	}

	/// <summary>
	/// Gets or sets flags for engine states during which this scene's visuals will be drawn and graphics rendered. If flags are 0, the scene will be skipped.<para/>
	/// NOTE: The only engine states that will draw scene contents are 'Loading', 'Running', and 'Unloading'. Only during running state should actual visuals happen;
	/// loading/unloading state updates should be strictly reserved for loading screens.<para/>
	/// HINT: A nice use-case of resetting all flags to 0 is to hide a scene, for example to load and populate its contents in the background, whilst another
	/// scene is still active and being drawn.
	/// </summary>
	public EngineState DrawnInEngineStates
	{
		get => drawnInEngineStates;
		set
		{
			if (value.HasFlag(EngineState.Startup) || value.HasFlag(EngineState.Shutdown))
			{
				logger.LogError("Only update flags for Loading, Unloading, and Running engine states are allowed! Resetting corresponding flags.");
				value &= ~(EngineState.Startup | EngineState.Shutdown);
			}
			if (drawnInEngineStates != 0 && value == 0) logger.LogMessage($"Disabling drawing of scene '{name}'...");
			else if (drawnInEngineStates == 0 && value != 0) logger.LogMessage($"Enabling drawing of scene '{name}'...");
			drawnInEngineStates = value;
		}
	}

	public int DrawStackState { get; private set; } = 0;

	/// <summary>
	/// Gets the total number of scene-wide behaviour components attached to this scene.
	/// </summary>
	public int SceneBehaviourCount => sceneBehaviours.Count;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _disposing)
	{
		bool wasDisposed = IsDisposed;
		IsDisposed = true;

		// Broadcast expiration event to all remaining scene objects:
		if (!wasDisposed)
		{
			BroadcastEvent(SceneEventType.OnSceneUnloaded, this, false);
		}

		if (graphicsStack != null)
		{
			// For for up to 100ms for current graphics operations to end:
			if (_disposing)
			{
				int i = 0;
				while(i++ < 100 && graphicsStack.IsDrawing)
				{
					Thread.Sleep(1);
				}
			}

			// Shut down and dispose graphics stack:
			graphicsStack.Shutdown();
			graphicsStack.Dispose();
		}

		// Clear out all update and drawing lists:
		drawManager.Dispose();
		updateManager.Dispose();

		// Dispose scene behaviours:
		foreach (SceneBehaviour component in sceneBehaviours)
		{
			component.Dispose();
		}

		// Dispose scene contents recursively:
		rootNode.Dispose();
	}

	public bool GetSceneBehaviour(int _index, out SceneBehaviour? _outBehaviour)
	{
		if (!IsDisposed && _index >= 0 && _index < sceneBehaviours.Count)
		{
			_outBehaviour = sceneBehaviours[_index];
			return !_outBehaviour.IsDisposed;
		}
		_outBehaviour = null;
		return false;
	}

	public bool GetSceneBehaviour<T>(out T? _outBehaviour) where T : SceneBehaviour
	{
		if (!IsDisposed)
		{
			foreach (SceneBehaviour behaviour in sceneBehaviours)
			{
				if (behaviour is not null && !behaviour.IsDisposed && behaviour is T typedBehaviour)
				{
					_outBehaviour = typedBehaviour;
					return true;
				}
			}
		}
		_outBehaviour = null;
		return false;
	}

	public bool CreateSceneBehaviour<T>(out T? _outNewBehaviour, params object[] _params) where T : SceneBehaviour
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot create new scene behaviour on disposed scene!");
			_outNewBehaviour = null;
			return false;
		}

		if (!SceneBehaviourFactory.CreateBehaviour(this, out _outNewBehaviour, _params) || _outNewBehaviour == null)
		{
			logger.LogError($"Failed to create new scene behaviour of type '{typeof(T)}' for scene '{name}'!");
			_outNewBehaviour = null;
			return false;
		}

		sceneBehaviours.Add(_outNewBehaviour);
		return false;
	}

	public bool AddSceneBehaviour(SceneBehaviour _newBehaviour)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot add behaviour to disposed scene!");
			return false;
		}
		if (_newBehaviour is null || _newBehaviour.IsDisposed)
		{
			logger.LogError($"Cannot add null or disposed behaviour to disposed scene '{name}'!");
			return false;
		}
		if (sceneBehaviours.Contains(_newBehaviour))
		{
			logger.LogError($"Scene behaviour '{_newBehaviour}' was already added to scene '{name}'!");
			return false;
		}

		sceneBehaviours.Add(_newBehaviour);
		return true;
	}

	/// <summary>
	/// Refresh all nodes in the scene, resetting and reevaulating states, clearing up expired data, and cleaning the scene graph..<para/>
	/// WARNING: This is a heavy and slow process that can easily lag the game, so use this extremely sparingly. Calling this before a hard
	/// save or when experiencing repeated errors and exceptions might be sensible.
	/// </summary>
	/// <param name="_enabledNodesOnly">Whether to only refresh nodes that are enabled in hierarchy. If false, all nodes will be refreshed.</param>
	public void Refresh(bool _enabledNodesOnly = true)
	{
		if (IsDisposed && !rootNode.IsDisposed) return;

		// Refresh scene-wide behaviours:
		foreach (SceneBehaviour behaviour in sceneBehaviours)
		{
			if (!behaviour.IsDisposed)
			{
				behaviour.Refresh();
			}
		}

		// Refresh all (enabled) nodes within the scene:
		IEnumerator<SceneNode> e = rootNode.IterateHierarchy(_enabledNodesOnly);
		while (e.MoveNext())
		{
			e.Current.Refresh();
		}

		// Reset/Reinitialize graphics stack last:
		graphicsStack?.Reset();
	}

	/// <summary>
	/// Get a list of all nodes within this scene. Nodes are gathered using a depth-first recursive search.
	/// </summary>
	/// <param name="_targetList">A list in which to store all nodes. Must be non-null, and will be cleared before any nodes are added.</param>
	/// <param name="_enabledOnly">Whether to only gather enabled nodes. If true, only nodes that are enabled and whose entire hierarchial branch
	/// up to this point was enabled are retrieved. If false, all nodes are gathered.</param>
	public void GetAllNodes(List<SceneNode> _targetList, bool _enabledOnly)
	{
		_targetList.Clear();
		if (IsDisposed) return;

		IEnumerator<SceneNode> e = rootNode.IterateHierarchy(_enabledOnly);
		while (e.MoveNext())
		{
			_targetList.Add(e.Current);
		}
	}

	/// <summary>
	/// Try to find a node by name, using a depth-first recursive search.
	/// </summary>
	/// <param name="_nodeName">The name of the node we're looking for. Names are case-sensitive. Search will quietly fail if the name is null.</param>
	/// <param name="_outNode">Outputs the first node matching the given name, or null, if no node of that name was found.</param>
	/// <returns>True if a node was found, false otherwise.</returns>
	public bool FindNode(string _nodeName, out SceneNode? _outNode)
	{
		if (!IsDisposed && !string.IsNullOrEmpty(_nodeName))
		{
			IEnumerator<SceneNode> e = rootNode.IterateHierarchy(false);
			while (e.MoveNext())
			{
				if (!e.Current.IsDisposed && string.CompareOrdinal(e.Current.Name, _nodeName) == 0)
				{
					_outNode = e.Current;
					return true;
				}
			}
		}
		_outNode = null;
		return false;
	}

	/// <summary>
	/// Try to find a node by a given criterion, using a depth-first recursive search.
	/// </summary>
	/// <param name="_funcSelector">Selector function delegate used to identify the node we're looking for. Must be non-null.</param>
	/// <param name="_outNode">Outputs the first node matching the given name, or null, if no node of that name was found.</param>
	/// <returns>True if a node was found, false otherwise.</returns>
	public bool FindNode(Func<SceneNode, bool> _funcSelector, out SceneNode? _outNode)
	{
		if (!IsDisposed)
		{
			if (_funcSelector is null)
			{
				logger.LogError("Selector function delegate may not be null!");
				_outNode = null;
				return false;
			}

			IEnumerator<SceneNode> e = rootNode.IterateHierarchy(false);
			while (e.MoveNext())
			{
				if (!e.Current.IsDisposed && _funcSelector(e.Current))
				{
					_outNode = e.Current;
					return true;
				}
			}
		}
		_outNode = null;
		return false;
	}

	/// <summary>
	/// Try to find the first occurrance of a specific component within the scene.
	/// </summary>
	/// <typeparam name="T">The type of the component we're looking for.</typeparam>
	/// <param name="_enabledOnly">Whether to only consider nodes that are currently enabled.</param>
	/// <param name="_outComponent">Outputs the first instance of the requested component type that is found, null if none were found.</param>
	/// <returns>True if a component was found, false otherwise.</returns>
	public bool FindComponentOfType<T>(bool _enabledOnly, out T? _outComponent) where T : Component
	{
		if (!IsDisposed)
		{
			_outComponent = rootNode.GetComponentInChildren<T>(_enabledOnly);
			if (_outComponent is not null && !_outComponent.IsDisposed)
			{
				return true;
			}
		}
		_outComponent = null;
		return false;
	}

	/// <summary>
	/// Find all instances of a specific component type within the scene.
	/// </summary>
	/// <typeparam name="T">The type of the component we're looking for.</typeparam>
	/// <param name="_targetList">A list in which to store all nodes. Must be non-null, and will be cleared before any nodes are added.</param>
	/// <param name="_enabledOnly">Whether to only consider nodes that are currently enabled.</param>
	/// <returns>True if any components were found, false otherwise.</returns>
	public bool FindAllComponentsOfType<T>(List<Component> _targetList, bool _enabledOnly) where T : Component
	{
		_targetList.Clear();
		if (IsDisposed) return false;

		IEnumerator<SceneNode> e = rootNode.IterateHierarchy(_enabledOnly);
		while (e.MoveNext())
		{
			T? component = e.Current.GetComponent<T>();
			if (component is not null && !component.IsDisposed)
			{
				_targetList.Add(component);
			}
		}
		return _targetList.Count != 0;
	}

	/// <summary>
	/// Sends a scene event to all nodes within the scene, starting from the root node.
	/// </summary>
	/// <param name="_eventType">The type of event that is being sent.</param>
	/// <param name="_eventData">Additional data describing or related to the event.</param>
	/// <param name="_enabledNodesOnly">Whether to only send the events to nodes that are enabled in hierarchy.</param>
	public void BroadcastEvent(SceneEventType _eventType, object? _eventData, bool _enabledNodesOnly)
	{
		if (IsDisposed) return;
		
		// Send event to scene-wide behaviours first:
		foreach (SceneBehaviour behaviour in sceneBehaviours)
		{
			if (!behaviour.IsDisposed)
			{
				behaviour.ReceiveSceneEvent(_eventType, _eventData);
			}
		}

		// Broadcast event down the scene hierarchy:
		rootNode.BroadcastEvent(_eventType, _eventData, _enabledNodesOnly);
	}

	public bool UpdateScene(SceneUpdateStage _stageFlag)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot update disposed scene!");
			return false;
		}

		return updateManager.RunUpdateStage(_stageFlag);
	}

	public bool DrawScene()
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot draw disposed scene!");
			return false;
		}

		// Ensure scene rendering resources are fully asssigned:
		graphicsStack ??= new ForwardPlusLightsStack(engine.GraphicsSystem.graphicsCore);
		spatialPartitioning ??= CreateFallbackSpatialPartitioningTree(drawManager.DrawListenerCount);

		spatialPartitioning.Clear(false);

		// Update scene-wide behaviours via event:
		foreach (SceneBehaviour behaviour in sceneBehaviours)
		{
			if (!behaviour.IsDisposed)
			{
				behaviour.Draw();
			}
		}

		return drawManager.RunDrawStage();
	}

	/// <summary>
	/// Creates a fallback spatial partitioning tree. This will be a very basic partitioning that is
	/// deferred to if no more specialized structure has been assigned to a scene via <see cref="SpatialPartitioning"/>.
	/// </summary>
	/// <param name="_minInitialCapacity">The minimum number of objects we're expecting the tree to contain.</param>
	/// <returns>A new instance of a basic spatial partitioning structure.</returns>
	internal static ISpatialTree CreateFallbackSpatialPartitioningTree(int _minInitialCapacity)
	{
		int initialCapacity = Math.Max(UnpartitionedTree.defaultObjectCapacity, _minInitialCapacity);

		ISpatialTree tree = new UnpartitionedTree(initialCapacity);
		return tree;
	}

	#endregion
}
