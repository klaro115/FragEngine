using FragEngine3.EngineCore;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes;

/// <summary>
/// The engine's scene management system. This class is a container that holds ownership of all scenes that have been
/// created in the engine. Each scene is a semi-isolated region of the app or game, that can have its own logic and
/// rendering setup.
/// </summary>
/// <param name="_engine">The engine whose scenes we will be managing.</param>
public sealed class SceneManager(Engine _engine) : IEngineSystem
{
	#region Constructors

	~SceneManager()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever a new scene is added to the scene manager.
	/// </summary>
	public event Action<Scene>? OnSceneAdded = null;
	/// <summary>
	/// Event that is triggered whenever a scene is removed from the scene manager. The scene may be diposed after this.
	/// </summary>
	public event Action<Scene>? OnSceneRemoved = null;

	#endregion
	#region Fields

	private readonly List<Scene> scenes = new(1);

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Engine Engine { get; } = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

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
	private void Dispose(bool _)
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
			Engine.Logger.LogError("Cannot add scene to disposed scene manager!");
			return false;
		}
		if (_newScene == null || _newScene.IsDisposed)
		{
			Engine.Logger.LogError("Cannot add null or disposed scene to manager!");
			return false;
		}
		if (scenes.Contains(_newScene))
		{
			Engine.Logger.LogError($"Scene '{_newScene.Name}' was already to manager!");
			return false;
		}

		scenes.Add(_newScene);

		BroadcastEvent(SceneEventType.OnSceneAdded, _newScene, true);
		OnSceneAdded?.Invoke(_newScene);
		return true;
	}

	public bool RemoveScene(Scene _scene, bool _disposeScene = true)
	{
		if (IsDisposed)
		{
			Engine.Logger.LogError("Cannot remove scene from disposed scene manager!");
			return false;
		}
		if (_scene == null)
		{
			Engine.Logger.LogError("Cannot remove nul scene from manager!");
			return false;
		}

		bool removed = scenes.Remove(_scene);
		if (removed)
		{
			BroadcastEvent(SceneEventType.OnSceneRemoved, _scene, true);
			OnSceneRemoved?.Invoke(_scene);
			// NOTE: 'OnSceneUnloaded' should be called before by whomever issued this call for removal, and after unloading all scene contents.
		}
		else
		{
			Engine.Logger.LogError($"Cannot remove scene '{_scene.Name}' from manager; it was not added to the manager.");
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
			Engine.Logger.LogError("Cannot broadcast event through disposed scene manager!");
			return false;
		}

		// Regular events are propagated across node hierarchy via recursion:
		foreach (Scene scene in scenes)
		{
			if (!scene.IsDisposed && scene.UpdatedInEngineStates.HasFlag(Engine.State))
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
			Engine.Logger.LogError("Cannot update scenes of disposed scene manager!");
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
			Engine.Logger.LogError("Cannot update scene of disposed scene manager!");
			return false;
		}
		if (_scene == null || _scene.IsDisposed)
		{
			Engine.Logger.LogError("Cannot update null or disposed scene!");
			return false;
		}

		return _scene.UpdateScene(_stage);
	}

	public bool DrawAllScenes(EngineState _engineState)
	{
		if (IsDisposed)
		{
			Engine.Logger.LogError("Cannot draw scenes of disposed scene manager!");
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
			Engine.Logger.LogError("Cannot draw scene of disposed scene manager!");
			return false;
		}
		if (_scene == null || _scene.IsDisposed)
		{
			Engine.Logger.LogError("Cannot draw null or disposed scene!");
			return false;
		}

		return _scene.DrawScene();
	}

	#endregion
}
