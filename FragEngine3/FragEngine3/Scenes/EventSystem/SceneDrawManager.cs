using FragEngine3.Graphics;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Stack;

namespace FragEngine3.Scenes.EventSystem
{
	/// <summary>
	/// Module of a <see cref="Scene"/> that manages registration of drawable scene elements, cameras, and light sources.
	/// This type also handles the scene's highest-level rendering logic via the scene's graphics stack (see '<see cref="Scene.GraphicsStack"/>').
	/// </summary>
	/// <param name="_scene">The scene this manager instance is attached to.</param>
	internal sealed class SceneDrawManager(Scene _scene) : IDisposable
	{
		#region Fields

		public readonly Scene scene = _scene ?? throw new ArgumentNullException(nameof(_scene), "Scene may not be null!");

		private readonly List<IRenderer> renderers = [];

		private readonly List<Camera> cameras = new(4);
		private readonly List<Light> lights = new(64);

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
			renderers.Clear();

			cameras.Clear();
			lights.Clear();
		}

		public bool RunDrawStage()
		{
			// No renderers in scene? Skip any further processing and return:
			if (DrawListenerCount == 0)
			{
				return true;
			}

			// Sort cameras and lights by priority. High-priority cameras will be drawn first, low-priority lights may be ignored:
			cameras.Sort((a, b) => a.cameraPriority.CompareTo(b.cameraPriority));
			lights.Sort((a, b) => a.lightPriority.CompareTo(b.lightPriority));

			// If null, create and initialize default forward+light graphics stack:
			if (scene.GraphicsStack == null || scene.GraphicsStack.IsDisposed)
			{
				scene.Logger.LogWarning($"Graphics stack of scene '{scene.Name}' is unassigned, creating default stack instead...");
				scene.GraphicsStack = new ForwardPlusLightsStack(scene.engine.GraphicsSystem.graphicsCore);
			}

			// Ensure the graphics stack is initialized before use:
			if (!scene.GraphicsStack.IsInitialized && !scene.GraphicsStack.Initialize(scene))
			{
				scene.Logger.LogError($"Failed to initialize graphics stack of scene '{scene.Name}'!");
				return false;
			}

			// Draw the scene and its nodes through the stack:
			return scene.GraphicsStack.DrawStack(scene, renderers, cameras, lights);
		}

		public bool RegisterRenderer(IRenderer _newRenderer)
		{
			if (_newRenderer == null || _newRenderer.IsDisposed)
			{
				scene.Logger.LogError("Cannot register null or disposed renderer for drawing!");
				return false;
			}

			if (!renderers.Contains(_newRenderer))
			{
				renderers.Add(_newRenderer);
			}
			return true;
		}

		public bool UnregisterRenderer(IRenderer _oldRenderer)
		{
			if (_oldRenderer == null)
			{
				scene.Logger.LogError("Cannot unregister null renderer for drawing!");
				return false;
			}

			renderers.Remove(_oldRenderer);
			return true;
		}

		public bool RegisterCamera(Camera _newCamera)
		{
			if (_newCamera == null || _newCamera.IsDisposed)
			{
				scene.Logger.LogError("Cannot register null or disposed camera in scene!");
				return false;
			}

			if (!cameras.Contains(_newCamera))
			{
				cameras.Add(_newCamera);
			}
			return true;
		}

		public bool UnregisterCamera(Camera _oldCamera)
		{
			if (_oldCamera == null)
			{
				scene.Logger.LogError("Cannot unregister null camera from scene!");
				return false;
			}

			cameras.Remove(_oldCamera);
			return true;
		}

		public bool RegisterLight(Light _newLight)
		{
			if (_newLight == null || _newLight.IsDisposed)
			{
				scene.Logger.LogError("Cannot register null or disposed light in scene!");
				return false;
			}

			if (!lights.Contains(_newLight))
			{
				lights.Add(_newLight);
			}
			return true;
		}

		public bool UnregisterLight(Light _oldLight)
		{
			if (_oldLight == null)
			{
				scene.Logger.LogError("Cannot unregister null light from scene!");
				return false;
			}

			lights.Remove(_oldLight);
			return true;
		}

		#endregion
	}
}
