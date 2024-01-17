using FragEngine3.EngineCore.Config;
using FragEngine3.EngineCore.Input;
using FragEngine3.EngineCore.Logging;
using FragEngine3.Graphics;
using FragEngine3.Resources;
using FragEngine3.Scenes;

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
			TimeManager = new TimeManager(this);
			ResourceManager = new ResourceManager(this);
			InputManager = new InputManager(this);
			GraphicsSystem = new GraphicsSystem(this);
			SceneManager = new SceneManager(this);
			//...

			SetState(EngineState.None, false, true);
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
		TimeSpan computeTimeSum = TimeSpan.Zero;
		TimeSpan frameTimeSum = TimeSpan.Zero;
		long stateStartFrameCount = 0;

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public bool IsRunning => !IsDisposed && State == EngineState.Running;
		public EngineState State { get; private set; } = EngineState.None;

		public Logger Logger { get; private set; } = null!;
		public PlatformSystem PlatformSystem { get; private set; } = null!;
		public TimeManager TimeManager { get; private set; } = null!;
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
			TimeManager?.Dispose();
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
			Logger.LogMessage("Exit was requested.");

			if (mainLoopCancellationSrc != null)
			{
				mainLoopCancellationSrc.Cancel();
			}
			else if (State == EngineState.Running)
			{
				SetState(EngineState.Unloading);
			}
		}

		private void SetState(EngineState _newState, bool _verbose = true, bool _force = false)
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

			if (stateChanged || _force)
			{
				// Log average frame timings of the previous state:
				long stateFrameCount = TimeManager.FrameCount - stateStartFrameCount;
				if (stateFrameCount > 0)
				{
					double avgFrameRate = stateFrameCount / frameTimeSum.TotalMilliseconds * 1000.0;
					double avgFrameTimeMs = frameTimeSum.TotalMilliseconds / stateFrameCount;
					double avgComputeTimeMs = computeTimeSum.TotalMilliseconds / stateFrameCount;

					Logger.LogMessage($"Engine state {prevState} | Average frame rate: {avgFrameRate:0.00} Hz | Average frame time: {avgFrameTimeMs:0.00} ms | Average compute time: {avgComputeTimeMs:0.00} ms");
				}
				stateStartFrameCount = TimeManager.FrameCount;
				frameTimeSum = TimeSpan.Zero;
				computeTimeSum = TimeSpan.Zero;

				// If requested, log state change:
				if (_verbose)
				{
					string message = State switch
					{
						EngineState.None => "### ENTERING STANDBY...",
						EngineState.Startup => "### STARTING UP...",
						EngineState.Loading => "### LOADING CONTENT...",
						EngineState.Running => "### RUNNING APPLICATION...",
						EngineState.Unloading => "### UNLOADING CONTENT...",
						EngineState.Shutdown => "### SHUTTING DOWN...",
						_ => "### wtf",
					};
					Logger.LogStatus(message);
				}

				// Ensure all OS and environment flags are up-to-date:
				PlatformSystem.UpdatePlatformFlags();

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

			TimeManager.Reset();

			bool success = true;

			try
			{
				// Run startup logic, loading configs and such:
				SetState(EngineState.Startup);
				//...

				// Run the main application loop:
				if (success)
				{
					success &= RunMainLoopStates();
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

		private bool RunMainLoopStates()
		{
			bool success = true;

			// Load content loop:
			success &= RunState_Loading(out bool continueToRunningState);
			if (!success)
			{
				Logger.LogError("Engine loading state failed, exiting!", -1, LogEntrySeverity.Critical);
			}
			
			// Main application loop:
			if (success && continueToRunningState)
			{
				success &= RunState_Running();
				if (!success)
				{
					Logger.LogError("Engine main running state failed, exiting!", -1, LogEntrySeverity.Critical);
				}
			}

			// Unload content loop:
			success &= RunState_Unloading();
			if (!success)
			{
				Logger.LogError("Engine unloading state failed, exiting!", -1, LogEntrySeverity.Critical);
			}

			return success;
		}

		private bool RunState_Loading(out bool _outContinueToRunningState)
		{
			// Start gathering resources and load data asynchronously:
			SetState(EngineState.Loading);

			if (!ResourceManager.fileGatherer.GatherAllResources(false, out Containers.Progress fileGatherProgress))
			{
				Logger.LogError("Error! Failed to start up resource manager!", _severity: LogEntrySeverity.Critical);
				_outContinueToRunningState = false;
				return false;
			}

			bool success = true;
			_outContinueToRunningState = true;

			// Run the main loop:
			while (success)
			{
				TimeManager.BeginFrame();

				// Update main window message loop, exit if an OS signal requested application quit:
				success &= GraphicsSystem.UpdateMessageLoop(out bool requestExit);
				if (requestExit && (mainLoopCancellationSrc == null || mainLoopCancellationSrc.IsCancellationRequested))
				{
					Exit();
				}

				// Respond to thread abort:
				if (mainLoopCancellationSrc != null && mainLoopCancellationSrc.IsCancellationRequested)
				{
					_outContinueToRunningState = false;
					break;
				}
				// Respond to loading process completion:
				else if (fileGatherProgress.IsFinished)
				{
					_outContinueToRunningState = true;
					break;
				}

				// Update application logic:
				success &= UpdateRuntimeLogic();
				
				// Sleep thread to target a consistent 60Hz:
				TimeManager.EndFrame(out TimeSpan threadSleepTime);
				computeTimeSum += TimeManager.LastFrameDuration;
				frameTimeSum += TimeManager.DeltaTime;
				Thread.Sleep(threadSleepTime.Milliseconds);
			}

			// Load or generate core resources for all systems:
			success &= GraphicsSystem.LoadBaseContent();
			//...

			return success;
		}

		private bool RunState_Running()
		{
			SetState(EngineState.Running);

			bool success = true;

			// Run the main loop:
			while (success)
			{
				TimeManager.BeginFrame();

				// Update main window message loop, exit if an OS signal requested application quit:
				success &= GraphicsSystem.UpdateMessageLoop(out bool requestExit);
				if (requestExit && (mainLoopCancellationSrc == null || mainLoopCancellationSrc.IsCancellationRequested))
				{
					Exit();
				}

				// Respond to thread abort:
				if (mainLoopCancellationSrc != null && mainLoopCancellationSrc.IsCancellationRequested)
				{
					break;
				}

				// Update application logic:
				success &= UpdateRuntimeLogic();
				
				// Sleep thread to target a consistent 60Hz:
				TimeManager.EndFrame(out TimeSpan threadSleepTime);
				computeTimeSum += TimeManager.LastFrameDuration;
				frameTimeSum += TimeManager.DeltaTime;
				Thread.Sleep(threadSleepTime.Milliseconds);

				//TODO [later]: Consider adding empty loop for delaying those last fractions of a millisecond.
			}

			return success;
		}

		private bool RunState_Unloading()
		{
			SetState(EngineState.Unloading);

			bool success = true;

			// Run the main loop:
			while (success)
			{
				TimeManager.BeginFrame();

				// Update main window message loop, exit if an OS signal requested application quit:
				success &= GraphicsSystem.UpdateMessageLoop(out bool requestExit);
				if (requestExit && (mainLoopCancellationSrc == null || mainLoopCancellationSrc.IsCancellationRequested))
				{
					Exit();
				}

				if (ResourceManager.TotalResourceCount == 0 && ResourceManager.TotalFileCount == 0)
				{
					break;
				}
				if (ResourceManager.QueuedResourceCount != 0)
				{
					ResourceManager.AbortAllImports();
				}

				const int maxUnloadsPerUpdate = 64;
				int unloadsThisUpdate = 0;

				// Unload and release resources:
				{
					IEnumerator<ResourceHandle> e = ResourceManager.IterateResources(false);
					while (e.MoveNext() && unloadsThisUpdate++ < maxUnloadsPerUpdate)
					{
						ResourceManager.RemoveResource(e.Current.resourceKey);
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

				// Update application logic:
				success &= UpdateRuntimeLogic();
				
				// Sleep thread to target a consistent 60Hz:
				TimeManager.EndFrame(out TimeSpan threadSleepTime);
				computeTimeSum += TimeManager.LastFrameDuration;
				frameTimeSum += TimeManager.DeltaTime;
				Thread.Sleep(threadSleepTime.Milliseconds);
			}

			// Make sure all resources are unloaded and no further imports are running:
			ResourceManager.AbortAllImports();
			ResourceManager.DisposeAllResources();

			return true;
		}

		private bool UpdateRuntimeLogic()
		{
			bool success = true;

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
			if (State == EngineState.Running)
			{
				success &= applicationLogic.DrawRunningState();
			}
			success &= SceneManager.DrawAllScenes(State);
			success &= GraphicsSystem.EndFrame();

			return success;
		}

		#endregion
	}
}
