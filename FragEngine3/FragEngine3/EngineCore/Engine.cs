using FragEngine3.EngineCore.Config;
using FragEngine3.EngineCore.EngineStates;
using FragEngine3.EngineCore.Input;
using FragEngine3.EngineCore.Logging;
using FragEngine3.Graphics;
using FragEngine3.Resources;
using FragEngine3.Scenes;

namespace FragEngine3.EngineCore;

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

		// Create all engine state instances:
		startupState = new(this, applicationLogic);
		loadingState = new(this, applicationLogic);
		runningState = new(this, applicationLogic);
		unloadingState = new(this, applicationLogic);
		shutdownState = new(this, applicationLogic);
		
		// Set initial standby/null state:
		SetState(EngineState.None, false, true);
		currentState = startupState;
	}

	~Engine()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly ApplicationLogic applicationLogic;
	private readonly EngineConfig config;

	// State machine:
	private EngineStateBase currentState;

	private readonly EngineStartupState startupState;
	private readonly EngineLoadingState loadingState;
	private readonly EngineRunningState runningState;
	private readonly EngineUnloadingState unloadingState;
	private readonly EngineShutdownState shutdownState;

	// Threading & exit conditions:
	private CancellationTokenSource? mainLoopCancellationSrc = null;
	private readonly object stateLockObj = new();

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
		InputManager?.Dispose();
		ResourceManager?.Dispose();
		TimeManager?.Dispose();
		PlatformSystem?.Dispose();
		//...

		mainLoopCancellationSrc?.Dispose();

		startupState.Dispose();
		loadingState.Dispose();
		runningState.Dispose();
		unloadingState.Dispose();
		shutdownState.Dispose();

		Logger?.Shutdown();
	}

	/// <summary>
	/// Gets the engine's main configuration data.
	/// </summary>
	/// <returns>A deep copy of the config. The config is strictly immutable at run-time.</returns>
	public EngineConfig GetEngineConfig() => config.Clone();

	/// <summary>
	/// Request the engine to stop the main loop and quit.<para/>
	/// This will end the program, make sure to save your progress before calling this ;)
	/// </summary>
	public void Exit()
	{
		Logger.LogMessage("Exit was requested.");

		if (mainLoopCancellationSrc is not null)
		{
			mainLoopCancellationSrc.Cancel();
		}
		else if (State == EngineState.Running)
		{
			SetState(EngineState.Unloading);
		}
	}

	/// <summary>
	/// Start up the engine and get the game/app going.
	/// </summary>
	/// <returns>True if the engine was start successfully, ran without issues, and then exited safely.
	/// False if it couldn't be started or if an unrecoverable error occured.</returns>
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
			success &= SetState(EngineState.Startup);
			success &= startupState.RunState(true);

			// Run the main application loops:
			if (success)
			{
				success &= RunMainLoopStates();
			}
			if (IsRunning) Exit();

			// Run shutdown logic, saving configs and such:
			success &= SetState(EngineState.Shutdown);
			success &= shutdownState.RunState(true);

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

	private bool SetState(EngineState _newState, bool _verbose = true, bool _force = false)
	{
		EngineState prevState;
		bool stateChanged;
		bool success = true;

		// Set the new state:
		lock (stateLockObj)
		{
			prevState = State;
			stateChanged = _newState != State;
			State = _newState;

			if (stateChanged || _force)
			{
				stateChanged = true;

				// End previous state:
				currentState?.EndState(_verbose);

				// Assign new state:
				currentState = State switch
				{
					EngineState.Startup => startupState,
					EngineState.Loading => loadingState,
					EngineState.Running => runningState,
					EngineState.Unloading => unloadingState,
					EngineState.Shutdown => shutdownState,
					_ => null!,
				};

				if (State != EngineState.None)
				{
					success &= currentState is not null && !currentState.IsDisposed;
					if (!success)
					{
						Logger.LogError($"Failed to switch to target engine state '{State}' from engine state '{prevState}'!");
						return false;
					}

					// Initialize new state:
					success &= currentState!.BeginState(mainLoopCancellationSrc, _verbose);
				}
			}
		}

		// Update states of application logic; exit if an error occures at this level:
		if (stateChanged || _force)
		{
			bool logicUpdated = applicationLogic.SetEngineState(prevState, _newState);
			if (!logicUpdated && IsRunning)
			{
				Exit();
			}
		}

		return success;
	}

	private bool RunMainLoopStates()
	{
		bool success = true;

		// Load content loop:
		success &= SetState(EngineState.Loading);

		success &= currentState.RunState(true);
		if (!success)
		{
			Logger.LogError("Engine loading state failed, exiting!", -1, LogEntrySeverity.Critical);
		}
		bool continueToRunningState = currentState == loadingState && loadingState.ContinueToRunningState;

		// Main application loop:
		if (success && continueToRunningState)
		{
			success &= SetState(EngineState.Running);

			success &= currentState.RunState(true);
			if (!success)
			{
				Logger.LogError("Engine main running state failed, exiting!", -1, LogEntrySeverity.Critical);
			}
		}

		// Unload content loop:
		success &= SetState(EngineState.Unloading);

		success &= currentState.RunState(true);
		if (!success)
		{
			Logger.LogError("Engine unloading state failed, exiting!", -1, LogEntrySeverity.Critical);
		}

		return success;
	}

	#endregion
}
