using FragEngine3.Graphics;
using FragEngine3.Resources;

namespace FragEngine3.EngineCore.EngineStates;

internal sealed class EngineUnloadingState(Engine _engine, ApplicationLogic _applicationLogic) : EngineStateBase(_engine, _applicationLogic)
{
	#region Properties

	protected override EngineState State => EngineState.Unloading;

	#endregion
	#region Methods

	protected override bool BeginState_internal(bool _verbose)
	{
		if (IsDisposed)
		{
			engine.Logger?.LogError("Cannot begin disposed engine unloading state!");
			return false;
		}

		// If requested, log state change:
		if (_verbose)
		{
			engine.Logger.LogStatus("### UNLOADING CONTENT...");
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
			engine.Logger?.LogError("Cannot update disposed engine unloading state!");
			return false;
		}

		return RunMainLoop();
	}

	protected override bool RunMainLoop()
	{
		bool success = true;

		ResourceManager resourceManager = engine.ResourceManager;
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

			if (resourceManager.TotalResourceCount == 0 && resourceManager.TotalFileCount == 0)
			{
				break;
			}
			if (resourceManager.QueuedResourceCount != 0)
			{
				resourceManager.AbortAllImports();
			}

			const int maxUnloadsPerUpdate = 64;
			int unloadsThisUpdate = 0;

			// Unload and release resources:
			{
				IEnumerator<ResourceHandle> e = resourceManager.IterateResources(false);
				while (e.MoveNext() && unloadsThisUpdate++ < maxUnloadsPerUpdate)
				{
					resourceManager.RemoveResource(e.Current.resourceKey);
				}
			}

			// Unload and close resource files afterwards:
			if (resourceManager.TotalResourceCount == 0)
			{
				IEnumerator<ResourceFileHandle> e = resourceManager.IterateFiles(false);
				while (e.MoveNext() && unloadsThisUpdate++ < maxUnloadsPerUpdate)
				{
					resourceManager.RemoveFile(e.Current.Key);
				}
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

		// Make sure all resources are unloaded and no further imports are running:
		resourceManager.AbortAllImports();
		resourceManager.DisposeAllResources();

		return true;
	}

	#endregion
}
