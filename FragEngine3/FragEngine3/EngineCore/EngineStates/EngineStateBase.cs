using FragEngine3.Graphics;
using FragEngine3.Scenes;

namespace FragEngine3.EngineCore.EngineStates;

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

	public bool IsDisposed { get; protected set; } = false;

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

	public void EndState(bool _verbose)
	{
		TimeManager timeManager = engine.TimeManager;

		// Log average frame timings of the previous state:
		long stateFrameCount = timeManager.FrameCount - stateStartFrameCount;
		if (stateFrameCount > 0)
		{
			double avgFrameRate = stateFrameCount / frameTimeSum.TotalMilliseconds * 1000.0;
			double avgFrameTimeMs = frameTimeSum.TotalMilliseconds / stateFrameCount;
			double avgComputeTimeMs = computeTimeSum.TotalMilliseconds / stateFrameCount;
			double slowFramePerc = (double)below40FpsFrameCount / stateFrameCount;

			engine.Logger.LogMessage($"Engine state {State} | Average frame rate: {avgFrameRate:0.00} Hz | Average frame time: {avgFrameTimeMs:0.00} ms | Average compute time: {avgComputeTimeMs:0.00} ms | Frames above 25 ms: {slowFramePerc:0.0}%");
		}
		stateStartFrameCount = timeManager.FrameCount;
		below40FpsFrameCount = 0;
		frameTimeSum = TimeSpan.Zero;
		computeTimeSum = TimeSpan.Zero;

		// Terminate any ongoing state logic and loops:
		EndState_internal(_verbose);
	}
	protected abstract void EndState_internal(bool _verbose);

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
			if (computeTimeSum.TotalMilliseconds > 25)
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

		SceneManager sceneManager = engine.SceneManager;
		GraphicsSystem graphicsSystem = engine.GraphicsSystem;

		// UPDATE:

		if (State == EngineState.Running)
		{
			success &= applicationLogic.UpdateRunningState();
		}

		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Fixed, State);

		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Early, State);
		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Main, State);
		success &= sceneManager.UpdateAllScenes(SceneUpdateStage.Late, State);

		// DRAW:

		success &= graphicsSystem.BeginFrame();
		if (State == EngineState.Running)
		{
			success &= applicationLogic.DrawRunningState();
		}
		success &= sceneManager.DrawAllScenes(State);
		success &= graphicsSystem.EndFrame();

		return success;
	}

	#endregion
}
