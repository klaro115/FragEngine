using FragEngine3.EngineCore.Logging;
using FragEngine3.Graphics;
using FragEngine3.Resources;

namespace FragEngine3.EngineCore.EngineStates;

internal sealed class EngineLoadingState(Engine _engine, ApplicationLogic _applicationLogic) : EngineStateBase(_engine, _applicationLogic)
{
	#region Fields

	public bool ContinueToRunningState { get; private set; } = false;

	#endregion
	#region Properties

	protected override EngineState State => EngineState.Loading;

	#endregion
	#region Methods

	protected override bool BeginState_internal(bool _verbose)
	{
		if (IsDisposed)
		{
			engine.Logger?.LogError("Cannot begin disposed engine loading state!");
			return false;
		}

		// If requested, log state change:
		if (_verbose)
		{
			engine.Logger.LogStatus("### LOADING CONTENT...");
		}

		//...
		return true;
	}

	protected override void EndState_internal(bool _verbose)
	{
		//...
	}

	public override bool RunState(bool _verbose)
	{
		if (IsDisposed)
		{
			engine.Logger?.LogError("Cannot update disposed engine loading state!");
			return false;
		}

		return RunMainLoop();
	}

	protected override bool RunMainLoop()
	{
		bool success = true;
		ContinueToRunningState = true;

		ResourceManager resourceManager = engine.ResourceManager;
		TimeManager timeManager = engine.TimeManager;
		GraphicsSystem graphicsSystem = engine.GraphicsSystem;

		if (!resourceManager.fileGatherer.GatherAllResources(false, out Containers.Progress fileGatherProgress))
		{
			engine.Logger.LogError("Error! Failed to start up resource manager!", _severity: LogEntrySeverity.Critical);
			ContinueToRunningState = false;
			return false;
		}

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
				ContinueToRunningState = false;
				break;
			}
			// Respond to loading process completion:
			else if (fileGatherProgress.IsFinished)
			{
				ContinueToRunningState = true;
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
		}

		// Load or generate core resources for all systems:
		success &= engine.GraphicsSystem.LoadBaseContent();
		//...

		return success;
	}

	#endregion
}
