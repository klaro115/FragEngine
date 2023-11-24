using FragEngine3.EngineCore;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Stack;
using FragEngine3.Scenes.EventSystem;
using System.Linq;
using System.Xml.Linq;

namespace FragEngine3.Scenes
{
	public sealed class Scene : IDisposable
	{
		#region Types

		private sealed class UpdateStage(SceneUpdateStage _stage)
		{
			public readonly SceneUpdateStage stage = _stage;
			public readonly List<SceneNode> nodeList = new(64);
		}

		#endregion
		#region Constructors

		public Scene(Engine _engine, string? _name = null)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
			rootNode = new SceneNode(this);
			if (_name != null) Name = _name;
		}
		~Scene()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		private string name = "Scene";

		public readonly Engine engine;
		public readonly SceneNode rootNode;

		private readonly List<SceneBehaviour> sceneBehaviours = [];
		private IGraphicsStack? graphicsStack = null;

		private readonly UpdateStage[] updateStageDict =
		[
			new(SceneUpdateStage.Early),
			new(SceneUpdateStage.Main),
			new(SceneUpdateStage.Late),
			new(SceneUpdateStage.Fixed),
		];
		private readonly List<SceneNodeRendererPair> sceneNodeRenderers = new(128);

		private readonly List<Camera> cameras = new(4);
		private readonly List<Light> lights = new(64);

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
		/// Gets the engine's logging module for error and debug output.
		/// </summary>
		public Logger Logger => engine.Logger ?? Logger.Instance!;

		/// <summary>
		/// Gets or sets the graphics stack to use for drawing this scene. Must be non-null. When assigning a new stack, the previous one is disposed.<para/>
		/// NOTE: If no stack is assigned when a call to '<see cref="DrawScene"/>' arrives, a default forward+light graphics stack without UI or post-processing pass
		/// is created instead.
		/// </summary>
		public IGraphicsStack? GraphicsStack
		{
			get => graphicsStack != null && !graphicsStack.IsDisposed ? graphicsStack : null;
			set
			{
				if (value != null && value.IsDisposed)
				{
					Logger.LogError("Scene graphics stack may not be disposed!");
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
					Logger.LogError("Only update flags for Loading, Unloading, and Running engine states are allowed! Resetting corresponding flags.");
					value &= ~(EngineState.Startup | EngineState.Shutdown);
				}
				if (updatedInEngineStates != 0 && value == 0) Logger.LogMessage($"Disabling updates of scene '{name}'...");
				else if (updatedInEngineStates == 0 && value != 0) Logger.LogMessage($"Enabling updates of scene '{name}'...");
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
					Logger.LogError("Only update flags for Loading, Unloading, and Running engine states are allowed! Resetting corresponding flags.");
					value &= ~(EngineState.Startup | EngineState.Shutdown);
				}
				if (drawnInEngineStates != 0 && value == 0) Logger.LogMessage($"Disabling drawing of scene '{name}'...");
				else if (drawnInEngineStates == 0 && value != 0) Logger.LogMessage($"Enabling drawing of scene '{name}'...");
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
			IsDisposed = true;

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
			if (_disposing)
			{
				foreach (UpdateStage stage in updateStageDict)
				{
					stage.nodeList.Clear();
				}
				sceneNodeRenderers.Clear();
			}

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
					if (behaviour != null && !behaviour.IsDisposed && behaviour is T typedBehaviour)
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
				Logger.LogError("Cannot create new scene behaviour on disposed scene!");
				_outNewBehaviour = null;
				return false;
			}

			if (!SceneBehaviour.CreateBehaviour(this, out _outNewBehaviour, _params) || _outNewBehaviour == null)
			{
				Logger.LogError($"Failed to create new scene behaviour of type '{typeof(T)}' for scene '{name}'!");
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
				Logger.LogError("Cannot add behaviour to disposed scene!");
				return false;
			}
			if (_newBehaviour == null || _newBehaviour.IsDisposed)
			{
				Logger.LogError($"Cannot add null or disposed behaviour to disposed scene '{name}'!");
				return false;
			}
			if (sceneBehaviours.Contains(_newBehaviour))
			{
				Logger.LogError($"Scene behaviour '{_newBehaviour}' was already added to scene '{name}'!");
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
				if (_funcSelector == null)
				{
					Logger.LogError("Selector function delegate may not be null!");
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
				if (_outComponent != null && !_outComponent.IsDisposed)
				{
					return true;
				}
			}
			_outComponent = null;
			return false;
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
				Logger.LogError("Cannot update disposed scene!");
				return false;
			}

			// Update scene-wide behaviours via event:
			SceneEventType eventType = _stageFlag.GetEventType();
			foreach (SceneBehaviour behaviour in sceneBehaviours)
			{
				if (!behaviour.IsDisposed)
				{
					behaviour.ReceiveSceneEvent(eventType, null);
				}
			}

			UpdateStage? updateStage = null;
			for (int i = 0; i < 4; ++i)
			{
				if (_stageFlag == (SceneUpdateStage)(1 << i))
				{
					updateStage = updateStageDict[i];
					break;
				}
			}

			if (updateStage == null)
			{
				Logger.LogError("Invalid update stage '{_stageFlag}'!");
				return false;
			}

			// Update all nodes and their components with listeners for this update event:
			foreach (SceneNode node in updateStage.nodeList)
			{
				node.SendEvent(eventType, null);
			}

			return true;
		}

		internal bool RegisterNodeForUpdateStages(SceneNode _node, SceneUpdateStage _stageFlags)
		{
			if (IsDisposed) return false;
			if (_node == null || _node.IsDisposed)
			{
				Logger.LogError("Cannot register update stages for null or disposed node");
				return false;
			}

			if (_stageFlags == 0)
			{
				return UnregisterNodeFromUpdateStages(_node);
			}

			for (int i = 0; i < 4; ++i)
			{
				SceneUpdateStage stage = (SceneUpdateStage)(i << i);

				if (_stageFlags.HasFlag(stage))
				{
					UpdateStage updateStage = updateStageDict[i];
					if (!updateStage.nodeList.Contains(_node))
					{
						updateStage.nodeList.Add(_node);
					}
				}
			}
			return true;
		}

		internal bool UpdateNodeUpdateStages(SceneNode _node, SceneUpdateStage _prevStageFlags, SceneUpdateStage _newStageFlags)
		{
			if (IsDisposed) return false;
			if (_node == null || _node.IsDisposed)
			{
				Logger.LogError("Cannot update registration of update stages for null or disposed node");
				return false;
			}

			if (_newStageFlags == 0)
			{
				return UnregisterNodeFromUpdateStages(_node);
			}

			for (int i = 0; i < 4; ++i)
			{
				SceneUpdateStage stage = (SceneUpdateStage)(i << i);
				bool isPrev = _prevStageFlags.HasFlag(stage);
				bool isNew = _newStageFlags.HasFlag(stage);

				if (isPrev && !isNew)
				{
					updateStageDict[i].nodeList.Remove(_node);
				}
				else if (!isPrev && isNew)
				{
					UpdateStage updateStage = updateStageDict[i];
					if (!updateStage.nodeList.Contains(_node))
					{
						updateStage.nodeList.Add(_node);
					}
				}
			}
			return true;
		}

		internal bool UnregisterNodeFromUpdateStages(SceneNode _node)
		{
			if (IsDisposed) return false;
			if (_node == null || _node.IsDisposed)
			{
				Logger.LogError("Cannot unregister update stages for null or disposed node");
				return false;
			}

			for (int i = 0; i < 4; ++i)
			{
				updateStageDict[i].nodeList.Remove(_node);
			}
			return true;
		}

		public bool DrawScene()
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot draw disposed scene!");
				return false;
			}

			// Update scene-wide behaviours via event:
			foreach (SceneBehaviour behaviour in sceneBehaviours)
			{
				if (!behaviour.IsDisposed)
				{
					behaviour.ReceiveSceneEvent(SceneEventType.OnDraw, null);
				}
			}

			// No renderers in scene? Skip any further processing and return:
			if (sceneNodeRenderers.Count == 0)
			{
				return true;
			}

			// Sort cameras and lights by priority. High-priority cameras will be drawn first, low-priority lights may be ignored:
			cameras.Sort((a, b) => a.cameraPriority.CompareTo(b.cameraPriority));
			lights.Sort((a, b) => a.lightPriority.CompareTo(b.lightPriority));

			// If null, create and initialize default forward+light graphics stack:
			if (graphicsStack == null || graphicsStack.IsDisposed)
			{
				Logger.LogWarning($"Graphics stack of scene '{name}' is unassigned, creating default stack instead...");
				graphicsStack = new ForwardPlusLightsStack(engine.GraphicsSystem.graphicsCore);
			}

			// Ensure the graphics stack is initialized before use:
			if (!graphicsStack.IsInitialized && !graphicsStack.Initialize(this))
			{
				Logger.LogError($"Failed to initialize graphics stack of scene '{name}'!");
				return false;
			}

			// Draw the scene and its nodes through the stack:
			return graphicsStack.DrawStack(this, sceneNodeRenderers, cameras, lights);
		}

		internal bool UnregisterNodeRenderers(SceneNode _node)
		{
			if (IsDisposed) return false;
			if (_node == null || _node.IsDisposed)
			{
				Logger.LogError("Cannot unregister draw stage for null or disposed node");
				return false;
			}

			bool removed = sceneNodeRenderers.RemoveAll(o => o.node == _node) != 0;
			if (removed)
			{
				DrawStackState++;
			}
			return removed;
		}

		internal bool RegisterNodeForDrawStage(SceneNode _node, IRenderer _renderer)
		{
			if (IsDisposed) return false;
			if (_node == null || _node.IsDisposed)
			{
				Logger.LogError("Cannot register draw stage for null or disposed node");
				return false;
			}
			if (_renderer == null || _renderer.IsDisposed)
			{
				Logger.LogError("Cannot unregister draw stage for null or disposed renderer");
				return false;
			}

			SceneNodeRendererPair newPair = new(_node, _renderer);

			if (!sceneNodeRenderers.Contains(newPair))
			{
				sceneNodeRenderers.Add(newPair);
				DrawStackState++;
			}
			return true;
		}
		
		internal bool UnregisterNodeFromDrawStage(SceneNode _node, IRenderer _renderer)
		{
			if (IsDisposed) return false;
			if (_node == null || _node.IsDisposed)
			{
				Logger.LogError("Cannot unregister draw stage for null or disposed node");
				return false;
			}
			if (_renderer == null || _renderer.IsDisposed)
			{
				Logger.LogError("Cannot unregister draw stage for null or disposed renderer");
				return false;
			}

			SceneNodeRendererPair oldPair = new(_node, _renderer);

			bool removed = sceneNodeRenderers.Remove(oldPair);
			if (removed)
			{
				DrawStackState++;
			}
			return removed;
		}

		internal bool RegisterCamera(Camera _camera)
		{
			if (IsDisposed) return false;
			if (_camera == null || _camera.IsDisposed)
			{
				Logger.LogError("Cannot register null or disposed camera!");
				return false;
			}
			
			if (!cameras.Contains(_camera))
			{
				cameras.Add(_camera);
			}
			return true;
		}

		internal bool UnregisterCamera(Camera _camera)
		{
			if (IsDisposed) return false;
			if (_camera == null)
			{
				Logger.LogError("Cannot unregister null camera!");
				return false;
			}

			return cameras.Remove(_camera);
		}

		internal bool RegisterLight(Light _light)
		{
			if (IsDisposed) return false;
			if (_light == null || _light.IsDisposed)
			{
				Logger.LogError("Cannot register null or disposed light source!");
				return false;
			}

			if (!lights.Contains(_light))
			{
				lights.Add(_light);
			}
			return true;
		}

		internal bool UnregisterLight(Light _light)
		{
			if (IsDisposed) return false;
			if (_light == null)
			{
				Logger.LogError("Cannot unregister null light source!");
				return false;
			}

			return lights.Remove(_light);
		}

		#endregion
	}
}
