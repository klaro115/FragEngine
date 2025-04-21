using FragEngine3.Graphics;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Stack;

namespace FragEngine3.Scenes.SceneManagers;

/// <summary>
/// Module of a <see cref="Scene"/> that manages registration of drawable scene elements, cameras, and light sources.
/// This type also handles the scene's highest-level rendering logic via the scene's graphics stack (see '<see cref="Scene.GraphicsStack"/>').
/// </summary>
/// <param name="_scene">The scene this manager instance is attached to.</param>
internal sealed class SceneDrawManager(Scene _scene) : IDisposable
{
	#region Fields

	public readonly Scene scene = _scene ?? throw new ArgumentNullException(nameof(_scene), "Scene may not be null!");

	private readonly List<IRenderer> renderers = [];        //TODO [later]: Split into multiple renderer groups that may populate command lists in parallel, each in their own thread.

	private readonly List<CameraComponent> cameras = new(4);
	private readonly List<ILightSource> lights = new(64);

	private readonly object lockObj = new();

	#endregion
	#region Properties

	public int DrawListenerCount => renderers.Count;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _disposing)
	{
		if (_disposing)
		{
			Clear();
		}
	}

	public void Clear()
	{
		lock (lockObj)
		{
			renderers.Clear();

			cameras.Clear();
			lights.Clear();
		}
	}

	public bool RunDrawStage()
	{
		// No renderers in scene? Skip any further processing and return:
		if (DrawListenerCount == 0)
		{
			return true;
		}

		// Sort cameras and lights by priority. High-priority cameras will be drawn first, low-priority lights may be ignored:
		lock(lockObj)
		{
			cameras.Sort((a, b) => a.cameraPriority.CompareTo(b.cameraPriority));
			lights.Sort((a, b) => a.LightPriority.CompareTo(b.LightPriority));
		}

		// If null, create and initialize default forward+light graphics stack:
		if (scene.GraphicsStack is null || scene.GraphicsStack.IsDisposed)
		{
			scene.engine.Logger.LogWarning($"Graphics stack of scene '{scene.Name}' is unassigned, creating default stack instead...");
			scene.GraphicsStack = new ForwardPlusLightsStack(scene.engine.GraphicsSystem.graphicsCore);
		}

		// Ensure the graphics stack is initialized before use:
		if (!scene.GraphicsStack.IsInitialized && !scene.GraphicsStack.Initialize(scene))
		{
			scene.engine.Logger.LogError($"Failed to initialize graphics stack of scene '{scene.Name}'!");
			return false;
		}

		// Draw the scene and its nodes through the stack:
		return scene.GraphicsStack.DrawStack(scene, renderers, cameras, lights);
	}

	public void SortDrawablesByRenderMode()
	{
		lock (lockObj)
		{
			renderers.Sort((a, b) => ((int)b.RenderMode).CompareTo((int)a.RenderMode));
		}
	}

	public bool RegisterRenderer(IRenderer _newRenderer)
	{
		if (_newRenderer is null || _newRenderer.IsDisposed)
		{
			scene.engine.Logger.LogError("Cannot register null or disposed renderer for drawing!");
			return false;
		}

		lock(lockObj)
		{
			if (!renderers.Contains(_newRenderer))
			{
				renderers.Add(_newRenderer);
			}
		}
		return true;
	}

	public bool UnregisterRenderer(IRenderer _oldRenderer)
	{
		if (_oldRenderer is null)
		{
			scene.engine.Logger.LogError("Cannot unregister null renderer for drawing!");
			return false;
		}

		lock (lockObj)
		{
			renderers.Remove(_oldRenderer);
		}
		return true;
	}

	public bool RegisterCamera(CameraComponent _newCamera)
	{
		if (_newCamera is null || _newCamera.IsDisposed)
		{
			scene.engine.Logger.LogError("Cannot register null or disposed camera in scene!");
			return false;
		}

		lock (lockObj)
		{
			if (!cameras.Contains(_newCamera))
			{
				cameras.Add(_newCamera);
			}
		}
		return true;
	}

	public bool UnregisterCamera(CameraComponent _oldCamera)
	{
		if (_oldCamera is null)
		{
			scene.engine.Logger.LogError("Cannot unregister null camera from scene!");
			return false;
		}

		lock (lockObj)
		{
			cameras.Remove(_oldCamera);
		}
		return true;
	}

	public bool RegisterLight(ILightSource _newLight)
	{
		if (_newLight is null || _newLight.IsDisposed)
		{
			scene.engine.Logger.LogError("Cannot register null or disposed light in scene!");
			return false;
		}

		lock (lockObj)
		{
			if (!lights.Contains(_newLight))
			{
				lights.Add(_newLight);
			}
		}
		return true;
	}

	public bool UnregisterLight(ILightSource _oldLight)
	{
		if (_oldLight is null)
		{
			scene.engine.Logger.LogError("Cannot unregister null light from scene!");
			return false;
		}

		lock (lockObj)
		{
			lights.Remove(_oldLight);
		}
		return true;
	}

	#endregion
}
