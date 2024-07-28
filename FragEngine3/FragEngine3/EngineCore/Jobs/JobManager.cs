namespace FragEngine3.EngineCore.Jobs;

public sealed class JobManager(Engine _engine) : IEngineSystem			//TODO: Call update/draw stage events from engine's mainloop states!
{
	#region Constructors

	~JobManager()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly Engine engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

	private readonly Dictionary<JobScheduleType, List<Job>> mainThreadJobQueues = new(4);	// Job queues for each schedule on the main thread.
	private readonly List<ThreadedJob> jobsInCustomThreads = new(1);
	private readonly List<Job> workerThreadJobs = new(4);
	private Job? currentWorkerThreadJob = null;

	private Thread? workerThread = null;
	private CancellationTokenSource? cancellationTokenSrc = new();

	private readonly object mainThreadLockObj = new();
	private readonly object customThreadLockObj = new();
	private readonly object workerThreadLockObj = new();

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Engine Engine => throw new NotImplementedException();

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	private void Dispose(bool disposing)
	{
		IsDisposed = true;

		ClearAllJobs(false);
		
		cancellationTokenSrc?.Dispose();
	}

	public void ClearAllJobs(bool _executeEndedCallbacks)
	{
		// Notify all thread of their cancellation:
		if (cancellationTokenSrc is not null && !cancellationTokenSrc.IsCancellationRequested)
		{
			cancellationTokenSrc.Cancel();
		}

		// Clear out main thread job queues:
		lock (mainThreadLockObj)
		{
			mainThreadJobQueues.Clear();
		}
		// Abort all ongoing jobs running in their own isolated threads:
		lock (customThreadLockObj)
		{
			foreach (ThreadedJob job in jobsInCustomThreads)
			{
				if (!job.IsDone) job.Abort(_executeEndedCallbacks);
				job.Dispose();
			}
			jobsInCustomThreads.Clear();
		}
		// Abort worker thread:
		lock (workerThreadLockObj)
		{
			// Interrupt any job currently in progress:
			currentWorkerThreadJob?.Abort(_executeEndedCallbacks);

			// Wait for the thread to exit safely:
			const int abortTimeout = 1000;
			const int abortSleepTimeMs = 10;
			int abortTimer = 0;
			while (workerThread is not null && workerThread.IsAlive && abortTimer < abortTimeout)
			{
				Thread.Sleep(abortSleepTimeMs);
				abortTimer += abortSleepTimeMs;
			}

			// Clear job queue:
			foreach (Job job in workerThreadJobs)
			{
				if (!job.IsDone) job.Abort(_executeEndedCallbacks);
			}
			workerThreadJobs.Clear();

			currentWorkerThreadJob = null;
			workerThread = null;
		}

		cancellationTokenSrc?.Dispose();
		cancellationTokenSrc = null;
	}

	public bool AddJob(FuncJobAction _funcJobAction, JobScheduleType _schedule = JobScheduleType.MainThread_MainUpdate, uint _priority = 0u)
	{
		if (_funcJobAction is null)
		{
			engine.Logger.LogError("Cannot schedule job using null action delegate!");
			return false;
		}

		// Ensure a cancellation source is ready to terminate threaded operations if necessary:
		cancellationTokenSrc ??= new();

		// A) Launch job in its own new thread:
		if (_schedule == JobScheduleType.NewThread)
		{
			ThreadedJob newJob = new(_funcJobAction, null, OnJobStatusChanged, cancellationTokenSrc.Token)
			{
				Schedule = _schedule,
				Priority = _priority,
			};
			lock (customThreadLockObj)
			{
				jobsInCustomThreads.Add(newJob);
			}
		}
		// B) Enqueue job for execution on main or worker thread:
		else
		{
			BasicJob newJob = new(_funcJobAction, null, OnJobStatusChanged)
			{
				Schedule = _schedule,
				Priority = _priority,
			};
			EnqueueJob(newJob);
		}
		return true;
	}

	private void EnqueueJob(Job _newJob)
	{
		// A) Main thread:
		if (_newJob.Schedule.IsScheduledOnMainThread())
		{
			lock (mainThreadLockObj)
			{
				// Fetch or create job queue:
				if (!mainThreadJobQueues.TryGetValue(_newJob.Schedule, out List<Job>? queue))
				{
					queue = new(10);
					mainThreadJobQueues.Add(_newJob.Schedule, queue);
				}

				SortedInsert(queue, _newJob);
			}
		}
		// B) Worker thread:
		else
		{
			lock (workerThreadLockObj)
			{
				SortedInsert(workerThreadJobs, _newJob);
			}
		}
	}

	private void OnJobStatusChanged(Job _job, bool _executeEndedCallback)
	{
		if (_job is null) return;

		// Optionally, notify the job's issuer:
		if (_executeEndedCallback && _job.funcJobEndedCallback is not null)
		{
			_job.funcJobEndedCallback.Invoke(_job.IsDone, !_job.IsError);
		}

		// If the job ran in its own thread, remove it from the list:
		if (_job.Schedule == JobScheduleType.NewThread && _job is ThreadedJob threadedJob)
		{
			lock (customThreadLockObj)
			{
				jobsInCustomThreads.Remove(threadedJob);
			}
			threadedJob.Dispose();
		}
		// ^NOTE: all other queues will dequeue and drop jobs automatically once they expire.
    }

	private static void SortedInsert(List<Job> _list, Job _newJob)		//TODO [important]: Reverse order, to go from highest to lowest instead!
	{
		// If list is empty, add directly:
		if (_list.Count == 0)
		{
			_list.Add(_newJob);
			return;
		}

		// If lowest priority, prepend to list:
		uint order = _newJob.Priority;
		if (order < _list[0].Priority)
		{
			_list.Insert(0, _newJob);
			return;
		}

		// If highest priority, append to list:
		int highIdx = _list.Count - 1;
		if (order >= _list[highIdx].Priority)
		{
			_list.Add(_newJob);
			return;
		}

		// Insert after searching for similarly weighted job priority, using Newton pattern:
		int lowIdx = 0;
		int midIdx = _list.Count / 2;
		uint mid = _list[midIdx].Priority;
		while (mid != order)
		{
			if (mid < order)
			{
				highIdx = midIdx;
			}
			else
			{
				lowIdx = midIdx;
			}
			midIdx = (highIdx + lowIdx) / 2;
			mid = _list[midIdx].Priority;
		}
		_list.Insert(midIdx, _newJob);
	}

	#endregion
}
