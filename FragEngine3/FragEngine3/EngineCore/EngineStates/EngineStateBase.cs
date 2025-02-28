using FragEngine3.EngineCore.Jobs;
using FragEngine3.Graphics;
using FragEngine3.Scenes;

namespace FragEngine3.EngineCore.EngineStates;

/// <summary>
/// Base type for classes that implement the state-specific logic of the engine's lifecycle state machine.
/// </summary>
/// <param name="_engine">The engine whose state machine this state belongs to.</param>
/// <param name="_applicationLogic">The engine's associated application logic controller.</param>
internal abstract class EngineStateBase(Engine _engine, ApplicationLogic _applicationLogic) : IDisposable
{
	#region Constructors

	~EngineStateBase()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly Engine engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
	private readonly ApplicationLogic applicationLogic = _applicationLogic ?? throw new ArgumentNullException(nameof(_applicationLogic), "Application logic may not be null!");

	protected CancellationTokenSource? mainLoopCancellationSrc = null;

	// Frame timings:
	protected TimeSpan computeTimeSum = TimeSpan.Zero;
	protected TimeSpan frameTimeSum = TimeSpan.Zero;
	protected long stateStartFrameCount = 0;
	protected long below40FpsFrameCount = 0;

	#endregion
	#region Properties

	/// <summary>
	/// Gets whether this state has been disposed already.
	/// </summary>
	public bool IsDisposed { get; protected set; } = false;

	/// <summary>
	/// Gets an enum value identifying this engine state.
	/// </summary>
	protected abstract EngineState State { get; }

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	protected virtual void Dispose(bool _disposing)
	{
		IsDisposed = true;
	}

	/// <summary>
	/// Initializes and activates the state.
	/// </summary>
	/// <param name="_mainLoopCancellationSrc">A cancellation source that is used to request an abort/cancellation of this state.</param>
	/// <param name="_verbose">Whether to log lots of additional status messages and warnings.</param>
	/// <returns>True if the state was initialized successfully and is now active, false otherwise.</returns>
	public bool BeginState(CancellationTokenSource? _mainLoopCancellationSrc, bool _verbose)
	{
		mainLoopCancellationSrc = _mainLoopCancellationSrc;

		if (!BeginState_internal(_verbose))
		{
			return false;
		}

		// Ensure all OS and environment flags are up-to-date:
		engine.PlatformSystem.UpdatePlatformFlags();

		return true;
	}
	protected abstract bool BeginState_internal(bool _verbose);

	/// <summary>
	/// Ends the state's activity.
	/// </summary>
	/// <param name="_verbose">Whether to log lots of additional status messages and warnings.</param>
	public void EndState(bool _verbose)
	{
		// Log average frame timings of the previous state:
		if (GenerateStateReport(out EngineStateReport report, true))
		{
			report.LogReport(engine.Logger);
		}

		// Terminate any ongoing state logic and loops:
		EndState_internal(_verbose);
	}
	protected abstract void EndState_internal(bool _verbose);

	/// <summary>
	/// Starts up the state's processes and main loop (assuming it has one).
	/// </summary>
	/// <param name="_verbose">Whether to log lots of additional status messages and warnings.</param>
	/// <returns>True if the state is running now, false otherwise.</returns>
	public abstract bool RunState(bool _verbose);

	protected virtual bool RunMainLoop()
	{
		bool success = true;

		TimeManager timeManager = engine.TimeManager;
		GraphicsSystem graphicsSystem = engine.GraphicsSystem;

		// Run the main loop:
		while (success)
		{
			timeManager.BeginFrame();

			// Update main window message loop, exit if an OS signal requested application quit:
			success &= graphicsSystem.UpdateMessageLoop(out bool requestExit);
			if (requestExit && (mainLoopCancellationSrc == null || mainLoopCancellationSrc.IsCancellationRequested))
			{
				engine.Exit();
			}

			// Respond to thread abort:
			if (mainLoopCancellationSrc != null && mainLoopCancellationSrc.IsCancellationRequested)
			{
				break;
			}

			// Update application logic:
			success &= UpdateRuntimeLogic();

			// Sleep thread to target a consistent 60Hz:
			timeManager.EndFrame(out TimeSpan threadSleepTime);
			computeTimeSum += timeManager.LastFrameDuration;
			frameTimeSum += timeManager.DeltaTime;
			if (timeManager.LastFrameDuration.TotalMilliseconds > 25)
			{
				below40FpsFrameCount++;
			}
			Thread.Sleep(threadSleepTime.Milliseconds);

			//TODO [later]: Consider adding empty loop for delaying those last fractions of a millisecond.
		}

		return success;
	}

	protected virtual bool UpdateRuntimeLogic()
	{
		bool success = true;

		JobManager jobManager = engine.JobManager;
		SceneManager sceneManager = engine.SceneManager;
		GraphicsSystem graphicsSystem = engine.GraphicsSystem;

		// UPDATE:

		if (State == EngineState.Running)
		{
			success &= applicationLogic.UpdateRunningState();
		}

		// Fixed Update:
		success &= jobManager.ProcessJobsOnMainThread(JobScheduleType.MainThread_FixedUpdate);
		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Fixed, State);

		// Early Update:
		success &= jobManager.ProcessJobsOnMainThread(JobScheduleType.MainThread_PreUpdate);
		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Early, State);

		// Main Update:
		success &= engine.HealthCheckSystem.MainUpdate();
		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Main, State);

		// Late Update:
		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Late, State);
		success &= jobManager.ProcessJobsOnMainThread(JobScheduleType.MainThread_PostUpdate);

		// DRAW:

		success &= jobManager.ProcessJobsOnMainThread(JobScheduleType.MainThread_PreDraw);
		success &= graphicsSystem.BeginFrame();

		if (State == EngineState.Running)
		{
			success &= applicationLogic.DrawRunningState();
		}
		success &= sceneManager.DrawAllScenes(State);

		success &= graphicsSystem.EndFrame();
		success &= jobManager.ProcessJobsOnMainThread(JobScheduleType.MainThread_PostDraw);

		return success;
	}

	/// <summary>
	/// Generates a rough performance report on for this engine state. This report includes frame rates, and other timing metrics about the main loop.<para/>
	/// Note: At least one frame must have passed since the last reset, or the report will be invalid.
	/// </summary>
	/// <param name="_outReport">Outputs a report containing metrics about the state's run-time performance.</param>
	/// <param name="_resetMetrics">Whether to reset counters and timers after generating the report.</param>
	/// <returns>True if a valid report could be created, false otherwise.</returns>
	public bool GenerateStateReport(out EngineStateReport _outReport, bool _resetMetrics = true)
	{
		if (IsDisposed)
		{
			_outReport = EngineStateReport.Zero;
			return false;
		}

		TimeManager timeManager = engine.TimeManager;

		long stateFrameCount = timeManager.FrameCount - stateStartFrameCount;
		if (stateFrameCount <= 0)
		{
			_outReport = EngineStateReport.Zero;
			return false;
		}
		
		_outReport = new()
		{
			State = State,
			ReportTimeUtc = DateTime.UtcNow,

			AvgFrameRate = stateFrameCount / frameTimeSum.TotalMilliseconds * 1000.0,
			AvgFrameTimeMs = frameTimeSum.TotalMilliseconds / stateFrameCount,
			AvgComputeTimeMs = computeTimeSum.TotalMilliseconds / stateFrameCount,
			SlowFramePerc = (double)below40FpsFrameCount / stateFrameCount,
		};

		if (_resetMetrics)
		{
			ResetStateMetrics();
		}
		return true;
	}

	private void ResetStateMetrics()
	{
		TimeManager timeManager = engine.TimeManager;

		stateStartFrameCount = timeManager.FrameCount;
		below40FpsFrameCount = 0;
		frameTimeSum = TimeSpan.Zero;
		computeTimeSum = TimeSpan.Zero;
	}

	#endregion
}
