using FragEngine3.EngineCore.Config;
using FragEngine3.EngineCore.Logging;
using FragEngine3.Graphics;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using System.Diagnostics;

namespace FragEngine3.EngineCore
{
    public sealed class Engine : IDisposable
	{
		#region Constructors

		public Engine(ApplicationLogic _applicationLogic, EngineConfig _config)
		{
			applicationLogic = _applicationLogic ?? throw new ArgumentNullException(nameof(_applicationLogic), "Application logic may not be null!");
			config = _config?.Clone() ?? throw new ArgumentNullException(nameof(_config), "Engine config may not be null!");

			// Create logger before any other module:
			Logger = new(this);
			if (!Logger.Initialize()) throw new ApplicationException("Logging system failed to initialize!");

			// Initialize application logic:
			applicationLogic.AssignEngine(this);

			// Create main engine modules:
			PlatformSystem = new PlatformSystem(this);
			ResourceManager = new ResourceManager(this);
			InputManager = new InputManager(this);
			GraphicsSystem = new GraphicsSystem(this);
			SceneManager = new SceneManager(this);
			//...

			SetState(EngineState.None, false);
		}
		~Engine()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		private readonly ApplicationLogic applicationLogic;
		private readonly EngineConfig config;

		// Threading & exit conditions:
		private CancellationTokenSource? mainLoopCancellationSrc = null;
		private readonly object stateLockObj = new();

		// Frame timings:
		private readonly Stopwatch stopwatch = new();
		private long frameStartTimeMs = 0;
		private long frameEndTimeMs = 0;
		const long frameTimeTargetMs = 1000 / 60;

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public bool IsRunning => !IsDisposed && State == EngineState.Running;
		public EngineState State { get; private set; } = EngineState.None;

		public Logger Logger { get; private set; } = null!;
		public PlatformSystem PlatformSystem { get; private set; } = null!;
		public ResourceManager ResourceManager { get; private set; } = null!;
		public InputManager InputManager { get; private set; } = null!;
		public GraphicsSystem GraphicsSystem { get; private set; } = null!;
		public SceneManager SceneManager { get; private set; } = null!;
		//...

		public static Engine? Instance { get; private set; } = null;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			if (_disposing && IsRunning) Exit();

			IsDisposed = true;

			SceneManager?.Dispose();
			GraphicsSystem?.Dispose();
			ResourceManager?.Dispose();
			PlatformSystem?.Dispose();
			//...

			mainLoopCancellationSrc?.Dispose();

			Logger?.Shutdown();
		}

		/// <summary>
		/// Gets the engine's main configuration data.
		/// </summary>
		/// <returns>A deep copy of the config. The config is strictly immutable at run-time.</returns>
		public EngineConfig GetEngineConfig() => config.Clone();

		public void Exit()
		{
			Logger.LogError("Exit was requested.");

			if (mainLoopCancellationSrc != null)
			{
				mainLoopCancellationSrc.Cancel();
			}
			else if (State == EngineState.Running)
			{
				SetState(EngineState.Unloading);
			}
		}

		private void SetState(EngineState _newState, bool _verbose = true)
		{
			EngineState prevState;
			bool stateChanged;

			// Set the new state:
			lock (stateLockObj)
			{
				prevState = State;
				stateChanged = _newState != State;
				State = _newState;
			}

			if (stateChanged)
			{
				// If requested, log state change:
				if (_verbose)
				{
					string message = State switch
					{
						EngineState.None => "\n### ENTERING STANDBY...",
						EngineState.Startup => "\n### STARTING UP...",
						EngineState.Loading => "\n### LOADING CONTENT...",
						EngineState.Running => "\n### RUNNING CONTENT...",
						EngineState.Unloading => "\n### UNLOADING CONTENT...",
						EngineState.Shutdown => "\n### SHUTTING DOWN...",
						_ => "\n### wtf",
					};
					Console.WriteLine(message);
				}

				// Update states of application logic; exit if an error occures at this level:
				bool logicUpdated = applicationLogic.SetEngineState(prevState, _newState);
				if (!logicUpdated && IsRunning)
				{
					Exit();
				}
			}
		}

		public bool Run()
		{
			if (State != EngineState.None)
			{
				Logger.LogError("Error! Engine is already running!");
				return false;
			}
			Instance ??= this;

			mainLoopCancellationSrc = new CancellationTokenSource();

			bool success = true;

			try
			{
				// Run startup logic, loading configs and such:
				SetState(EngineState.Startup);
				//...

				// Run the main application loop:
				if (success)
				{
					success &= RunMainLoop();
				}
				if (IsRunning) Exit();

				// Run shutdown logic, saving configs and such:
				SetState(EngineState.Shutdown);
				//...

				SetState(EngineState.None);
			}
			catch (Exception ex)
			{
				Logger.LogException("An unhandled exception caused the engine's main loop to crash!", ex, LogEntrySeverity.Critical);
				SetState(EngineState.None);
				success = false;
			}

			mainLoopCancellationSrc?.Dispose();
			mainLoopCancellationSrc = null;

			if (Instance == this) Instance = null;
			return success;
		}

		private bool RunMainLoop()
		{
			stopwatch.Restart();

			// Start gathering resources and load data asynchronously:
			SetState(EngineState.Loading);

			if (!ResourceManager.GatherAllResourceFiles(false, out Containers.Progress fileGatherProgress))
			{
				Logger.LogError("Error! Failed to start up resource manager!", _severity: LogEntrySeverity.Critical);
				return false;
			}

			bool success = true;

			// Run the main loop across the entire loading and application run-time stages:
			while (success)
			{
				frameStartTimeMs = stopwatch.ElapsedMilliseconds;

				// Update main window message loop, exit if an OS signal requested application quit:
				success &= GraphicsSystem.UpdateMessageLoop(out bool requestExit);
				if (requestExit && (mainLoopCancellationSrc == null || mainLoopCancellationSrc.IsCancellationRequested))
				{
					Exit();
				}

				// 1. Wait for static application resources loading to finish:
				if (State == EngineState.Loading)
				{
					if (mainLoopCancellationSrc != null && mainLoopCancellationSrc.IsCancellationRequested)
					{
						SetState(EngineState.Unloading);
						continue;
					}
					else if (fileGatherProgress.IsFinished)
					{
						SetState(EngineState.Running);
						continue;
					}
					success &= UpdateRuntimeLogic();
				}
				// 2. Run the main game logic on repeat:
				else if (State == EngineState.Running)
				{
					if (mainLoopCancellationSrc != null && mainLoopCancellationSrc.IsCancellationRequested)
					{
						SetState(EngineState.Unloading);
						continue;
					}
					success &= UpdateRuntimeLogic();
				}
				// 3. Wait for resouces to finish unloading:
				else if (State == EngineState.Unloading)
				{
					if (ResourceManager.TotalResourceCount == 0 && ResourceManager.TotalFileCount == 0)
					{
						break;
					}
					if (ResourceManager.QueuedResourceCount != 0)
					{
						ResourceManager.AbortAllImports();
					}

					const int maxUnloadsPerUpdate = 32;
					int unloadsThisUpdate = 0;

					// Unload and release resources:
					{
						IEnumerator<ResourceHandle> e = ResourceManager.IterateResources(false);
						while (e.MoveNext() && unloadsThisUpdate++ < maxUnloadsPerUpdate)
						{
							ResourceManager.RemoveResource(e.Current.Key);
						}
					}

					// Unload and close resource files afterwards:
					if (ResourceManager.TotalResourceCount == 0)
					{
						IEnumerator<ResourceFileHandle> e = ResourceManager.IterateFiles(false);
						while (e.MoveNext() && unloadsThisUpdate++ < maxUnloadsPerUpdate)
						{
							ResourceManager.RemoveFile(e.Current.Key);
						}
					}

					success &= UpdateRuntimeLogic();
				}
				else break;

				// Sleep thread to target a consistent 60Hz:
				frameEndTimeMs = stopwatch.ElapsedMilliseconds;
				long elapsedFrameTimeMs = frameEndTimeMs - frameStartTimeMs;
				long sleepTime = Math.Max(frameTimeTargetMs - elapsedFrameTimeMs, 0);
				Thread.Sleep((int)sleepTime);
			}

			SetState(EngineState.Unloading);

			// Make sure all resources are unloaded and no further imports are running:
			ResourceManager.AbortAllImports();
			ResourceManager.DisposeAllResources();

			stopwatch.Stop();
			return success;
		}

		private bool UpdateRuntimeLogic()
		{
			bool success = true;

			//TEST TEST TEST TEST
			if (stopwatch.ElapsedMilliseconds > 2500 && State == EngineState.Running)
			{
				Exit();
			}

			// UPDATE:

			if (State == EngineState.Running)
			{
				success &= applicationLogic.UpdateRunningState();
			}

			success &= SceneManager.UpdateAllScenes(SceneUpdateStage.Fixed, State);

			success &= SceneManager.UpdateAllScenes(SceneUpdateStage.Early, State);
			success &= SceneManager.UpdateAllScenes(SceneUpdateStage.Main, State);
			success &= SceneManager.UpdateAllScenes(SceneUpdateStage.Late, State);

			// DRAW:

			success &= GraphicsSystem.BeginFrame();
			success &= SceneManager.DrawAllScenes(State);
			success &= GraphicsSystem.EndFrame();

			return success;
		}

		#endregion
	}
}
