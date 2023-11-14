using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using FragEngine3.Scenes.EventSystem;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public sealed class ForwardPlusLightsStack : IGraphicsStack
	{
		#region Constructors

		public ForwardPlusLightsStack(GraphicsCore _core)
		{
			core = _core ?? throw new ArgumentNullException(nameof(core), "Graphics core may not be null!");
		}
		~ForwardPlusLightsStack()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsCore core;

		private bool isInitialized = false;
		private bool isDrawing = false;

		private readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public bool IsValid => !IsDisposed && core.IsInitialized && Scene != null && !Scene.IsDisposed;	//TODO
		public bool IsInitialized => !IsDisposed && isInitialized;
		public bool IsDrawing => IsInitialized && isDrawing;

		public Scene? Scene { get; private set; } = null;

		private Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			if (isInitialized)
			{
				Shutdown();
			}

			IsDisposed = true;

			//...
		}

		public bool Initialize(Scene _scene)
		{
			lock(lockObj)
			{
				if (IsDisposed)
				{
					Logger.LogError("Cannot initialize disposed Forward+Lights graphics stack!");
					return false;
				}
				if (_scene == null || _scene.IsDisposed)
				{
					Logger.LogError("Cannot initialize graphics stack for null or disposed scene!");
					return false;
				}

				Scene = _scene;

				//...

				Logger.LogMessage($"Initialized graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");

				isInitialized = true;
			}
			return true;
		}

		public void Shutdown()
		{
			if (!isInitialized) return;

			lock(lockObj)
			{
				//...

				Scene = null;

				isDrawing = false;
				isInitialized = false;
			}

			Logger.LogMessage($"Shut down graphics stack of type '{nameof(ForwardPlusLightsStack)}'.");
		}

		public bool Reset()
		{
			Logger.LogMessage($"Resetting graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene?.Name ?? "NULL"}'.");

			if (IsDisposed)
			{
				Logger.LogError("Cannot reset disposed Forward+Lights graphics stack!");
				return false;
			}

			// Simply shut down the whole stack:
			Shutdown();

			if (!IsValid)
			{
				Logger.LogError("Cannot reinitialize invalid Forward+Lights graphics stack!");
				return false;
			}

			// Then reinitialize it back to its starting state:
			return Initialize(Scene!);
		}

		public bool GetRenderTargets(out Framebuffer? _outRenderTargets)
		{
			throw new NotImplementedException();
		}

		public bool DrawStack(Scene _scene, List<SceneNode> _drawnNodes)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot draw uninitialized Forward+Lights graphics stack!");
				return false;
			}
			if (_scene != Scene || Scene.IsDisposed)
			{
				Logger.LogError("Cannot draw graphics stack for null or mismatched scene!");
				return false;
			}
			if (_drawnNodes == null)
			{
				Logger.LogError("Cannot draw graphics stack for null list of scene nodes!");
				return false;
			}

			// No nodes and no scene behaviours? Skip drawing altogether:
			if (_drawnNodes.Count == 0 && _scene.SceneBehaviourCount == 0)
			{
				return true;
			}

			isDrawing = true;
			lock(lockObj)
			{

				// Update all nodes and their components with listeners for this update event:
				foreach (SceneNode node in _drawnNodes)
				{
					node.SendEvent(SceneEventType.OnDraw, null);				// TODO: Change this to no longer use the event system for gernerating draw calls! Register renderers directly!
				}

			}
			isDrawing = false;
			return true;
		}

		#endregion
	}
}
