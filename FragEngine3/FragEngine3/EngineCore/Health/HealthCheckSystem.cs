using FragEngine3.EngineCore.Logging;

namespace FragEngine3.EngineCore.Health;

public sealed class HealthCheckSystem : IDisposable
{
	#region Constructors

	public HealthCheckSystem(Engine _engine)
	{
		engine = _engine;

		mainThreadQueue = new(engine, AbortConditionCallback, WarningConditionCallback);
		checkThreadQueue = new(engine, AbortConditionCallback, WarningConditionCallback);
	}

	~HealthCheckSystem()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly Engine engine;

	private Thread? checkThread = null;
	private CancellationTokenSource? cancellationSrc = null;
	private readonly CancellationTokenSource engineAbortSrc = new();

	private uint abortEventCounter = 0;
	private uint warningEventCounter = 0;

	private readonly HealthCheckQueue mainThreadQueue;
	private readonly HealthCheckQueue checkThreadQueue;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
	{
		IsDisposed = true;

		if (checkThread is not null && checkThread.IsAlive)
		{
			StopThread();
		}

		cancellationSrc?.Dispose();
		engineAbortSrc.Dispose();
	}

	/// <summary>
	/// Stops the thread used for asynchronous checks.
	/// </summary>
	private void StopThread()
	{
		if (checkThread is null)
		{
			return;
		}

		cancellationSrc?.Cancel();

		int threadEndTimer = 0;
		const int threadTimeout = 500;
		while (checkThread.IsAlive && threadEndTimer < threadTimeout)
		{
			Thread.Sleep(10);
		}
		//checkThread.Abort();
		checkThread = null;

		cancellationSrc?.Dispose();
		cancellationSrc = null;
	}

	/// <summary>
	/// Schedules a new check.
	/// </summary>
	/// <param name="_newCheck">The new check we want to add.</param>
	/// <returns>True if the check could be scheduled successfully, false otherwise.</returns>
	public bool AddCheck(HealthCheck _newCheck)
	{
		if (IsDisposed)
		{
			return false;
		}
		if (_newCheck is null || !_newCheck.IsValid())
		{
			engine.Logger.LogError("Cannot add null or invalid sanity check!");
			return false;
		}

		if (_newCheck.executeOnMainThread)
		{
			// Queue up check on the main thread:
			return mainThreadQueue.AddCheck(_newCheck);
		}
		else
		{
			// (Re)start a seperate check thread:
			if (checkThread is null || !checkThread.IsAlive)
			{
				cancellationSrc?.Dispose();
				cancellationSrc = new();

				checkThread = new(RunThread);
				checkThread.Start();
			}
			// Queue up check on the check thread:
			return checkThreadQueue.AddCheck(_newCheck);
		}

		return true;
	}

	/// <summary>
	/// Unschedules and cancels a specific check.
	/// </summary>
	/// <param name="_check">The check we want to remove.</param>
	/// <returns>True if the check was scheduled and removed, false otherwise.</returns>
	public bool RemoveCheck(HealthCheck _check)
	{
		if (IsDisposed || _check is null)
		{
			return false;
		}

		bool removed = _check.executeOnMainThread
			? mainThreadQueue.RemoveCheck(_check)
            : checkThreadQueue.RemoveCheck(_check);
		return removed;
	}

	/// <summary>
	/// Unschedules and cancels a check by its ID.
	/// </summary>
	/// <param name="_checkId">The unique ID of the check we want to remove.</param>
	/// <returns>True if the check was scheduled and removed, false otherwise.</returns>
	public bool RemoveCheck(int _checkId)
	{
		if (IsDisposed)
		{
			return false;
		}

		bool removed = mainThreadQueue.RemoveCheck(_checkId) || checkThreadQueue.RemoveCheck(_checkId);
		return removed;
	}

	public void RemoveAllChecks()
	{
		if (IsDisposed)
		{
			return;
		}

		StopThread();

		mainThreadQueue.RemoveAllChecks();
		checkThreadQueue.RemoveAllChecks();
	}

	/// <summary>
	/// Executes checks that were scheduled on the main thread. At most one check is performed per update cycle.
	/// This method is called exclusively during the main update stage of the engine's main loop. The engine is shut down
	/// and will exit if any check encountered an abort condition since the last call.
	/// </summary>
	/// <returns>True if checks were processed as usual, or false, if this instance has ben disposed.</returns>
	internal bool MainUpdate()
	{
		if (IsDisposed)
		{
			return false;
		}
		// Request full engine shutdown if an abort condition was hit:
		if (engineAbortSrc.IsCancellationRequested)
		{
			engine.Logger.LogError("Abort condition detected, engine health may be compromised! Requesting engine shutdown.", _severity: LogEntrySeverity.Critical);
			engine.Logger.LogWarning($"Health check system has detected a total of {warningEventCounter} warning events, and {abortEventCounter} abort events.");
			engine.Exit();
			return true;
		}

		if (!mainThreadQueue.PeekCheck(out HealthCheck? currentCheck))
		{
			return true;
		}

		return checkThreadQueue.PopAndExecuteCheck(currentCheck!);
	}

	/// <summary>
	/// Main loop method of the separate check thread.
	/// </summary>
	private void RunThread()
	{
		// Keep looping until either the queue is empty, or the thread was aborted:
		while (!IsDisposed && checkThreadQueue.Count != 0 && cancellationSrc is not null && !cancellationSrc.IsCancellationRequested)
		{
			// Repeatedly wait until the next check's time has come:
			if (!checkThreadQueue.PeekCheck(out HealthCheck? currentCheck))
			{
				break;
			}

			if (DateTime.UtcNow < currentCheck.nextCheckTime) //TODO: Use timestamps from TimeManager instead of DateTime.UtcNow!
			{
				// If the check is not yet due, wait up to 10 milliseconds before peeking again:
				int sleepTimeMs = Math.Clamp((int)(currentCheck.nextCheckTime - DateTime.UtcNow).TotalMilliseconds, 1, 10);
				Thread.Sleep(sleepTimeMs);
				continue;
			}

			// Execute the check; exit loop immediately if an abort condition was hit:
			if (checkThreadQueue.PopAndExecuteCheck(currentCheck))
			{
				break;
			}

			Thread.Sleep(1);
		}

		// Invalidate cancellation source:
		if (cancellationSrc is not null && !cancellationSrc.IsCancellationRequested)
		{
			cancellationSrc.Cancel();
		}
		checkThread = null;
	}

	private void AbortConditionCallback(HealthCheck _failedCheck)
	{
		engine.Logger.LogError($"Engine sanity check failed: {_failedCheck} - Full abort required!");
		cancellationSrc?.Cancel();
		engineAbortSrc.Cancel();
		abortEventCounter++;
	}

	private void WarningConditionCallback(HealthCheck _problematicCheck)
	{
		engine.Logger.LogWarning($"Engine sanity check failed: {_problematicCheck} - Some systems may be unstable!");
		warningEventCounter++;
	}

	#endregion
}
