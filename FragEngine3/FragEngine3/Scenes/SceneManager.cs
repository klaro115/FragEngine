using FragEngine3.EngineCore;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes
{
	public sealed class SceneManager : IDisposable
	{
		#region Constructors

		public SceneManager(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		}
		~SceneManager()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine;

		private readonly List<Scene> scenes = new(1);

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		/// <summary>
		/// Gets the total number of scenes assigned to the manager.
		/// </summary>
		public int TotalSceneCount => scenes.Count;
		/// <summary>
		/// Gets the total number of scenes that are active, alive, and set to update.
		/// </summary>
		public int ActiveSceneCount => scenes.Count(o => !o.IsDisposed && o.UpdatedInEngineStates != 0);

		/// <summary>
		/// Gets the first active scene that is not disposed and is set to update.
		/// </summary>
		public Scene? MainScene => scenes.FirstOrDefault(o => !o.IsDisposed && o.UpdatedInEngineStates != 0);

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
			for (int i = 0; i < scenes.Count; i++)
			{
				scenes[i].Dispose();
			}
		}

		public bool AddScene(Scene _newScene)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot add scene to disposed scene manager!");
				return false;
			}
			if (_newScene == null || _newScene.IsDisposed)
			{
				Console.WriteLine("Error! Cannot add null or disposed scene to manager!");
				return false;
			}
			if (scenes.Contains(_newScene))
			{
				Console.WriteLine($"Error! Scene '{_newScene.Name}' was already to manager!");
				return false;
			}

			scenes.Add(_newScene);

			BroadcastEvent(SceneEventType.OnSceneAdded, _newScene, true);
			return true;
		}

		public bool RemoveScene(Scene _scene, bool _disposeScene = true)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot remove scene from disposed scene manager!");
				return false;
			}
			if (_scene == null)
			{
				Console.WriteLine("Error! Cannot remove nul scene from manager!");
				return false;
			}

			bool removed = scenes.Remove(_scene);
			if (removed)
			{
				BroadcastEvent(SceneEventType.OnSceneRemoved, _scene, true);
				// NOTE: 'OnSceneUnloaded' should be called before by whomever issued this call for removal, and after unloading all scene contents.
			}
			else
			{
				Console.WriteLine($"Error! Cannot remove scene '{_scene.Name}' from manager; it was not added to the manager.");
			}

			if (_disposeScene)
			{
				_scene.Dispose();
			}
			return removed;
		}

		public bool BroadcastEvent(SceneEventType _eventType, object? _eventData, bool _enabledNodesOnly)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot broadcast event through disposed scene manager!");
				return false;
			}

			// Handle special event types: (Update & Draw)
			if (_eventType.IsUpdateStage())
			{
				return UpdateAllScenes(_eventType.GetUpdateStage(), engine.State);
			}
			if (_eventType == SceneEventType.OnDraw)
			{
				return DrawAllScenes(engine.State);
			}

			// Regular events are propagated across node hierarchy via recursion:
			foreach (Scene scene in scenes)
			{
				if (!scene.IsDisposed && scene.UpdatedInEngineStates.HasFlag(engine.State))
				{
					scene.BroadcastEvent(_eventType, _eventData, _enabledNodesOnly);
				}
			}
			return true;
		}

		public bool UpdateAllScenes(SceneUpdateStage _stage, EngineState _engineState)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot update scenes of disposed scene manager!");
				return false;
			}

			bool success = true;

			foreach (Scene scene in scenes)
			{
				if (!scene.IsDisposed && scene.UpdatedInEngineStates.HasFlag(_engineState))
				{
					success &= UpdateScene(scene, _stage);
				}
			}

			return success;
		}

		public bool UpdateScene(Scene _scene, SceneUpdateStage _stage)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot update scene of disposed scene manager!");
				return false;
			}
			if (_scene == null || _scene.IsDisposed)
			{
				Console.WriteLine("Error! Cannot update null or disposed scene!");
				return false;
			}

			return _scene.UpdateScene(_stage);
		}

		public bool DrawAllScenes(EngineState _engineState)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot draw scenes of disposed scene manager!");
				return false;
			}

			bool success = true;

			foreach (Scene scene in scenes)
			{
				if (!scene.IsDisposed && scene.DrawnInEngineStates.HasFlag(_engineState))
				{
					success &= DrawScene(scene);
				}
			}

			return success;
		}

		public bool DrawScene(Scene _scene)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot draw scene of disposed scene manager!");
				return false;
			}
			if (_scene == null || _scene.IsDisposed)
			{
				Console.WriteLine("Error! Cannot draw null or disposed scene!");
				return false;
			}

			return _scene.DrawScene();
		}

		#endregion
	}
}
