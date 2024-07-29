using FragEngine3.Containers;
using System.Diagnostics;

namespace FragEngine3.EngineCore.Jobs;

public sealed class JobManager : IEngineSystem
{
	#region Constructors

	public JobManager(Engine _engine)
	{
		Engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

		stopwatch.Start();
	}

	~JobManager()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly Dictionary<JobScheduleType, List<Job>> mainThreadJobQueues = new(4);		//TODO: Change lists to act as stacks instead; shifting around all contents when dequeueing is kinda bad.
	private readonly List<ThreadedJob> jobsInCustomThreads = new(1);
	private readonly List<Job> workerThreadJobs = new(4);
	private Job? currentWorkerThreadJob = null;

	private readonly Stopwatch stopwatch = new();
	private Thread? workerThread = null;
	private CancellationTokenSource? cancellationTokenSrc = new();

	private long maxMillisecondsPerUpdateStage = 4;
	private int maxJobsPerUpdateStage = 20;

	private readonly object mainThreadLockObj = new();
	private readonly object customThreadLockObj = new();
	private readonly object workerThreadLockObj = new();

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	/// <summary>
	/// Gets or sets the maximum time that may be spent executing jobs on the main thread, per update/draw stage, in milliseconds.
	/// Working through too many jobs may impact frame rate, therefore no more than a few milliseconds should be spent on jobs per
	/// stage, so that a minimum of 60 FPS can be be guaranteed at all times.
	/// </summary>
	public long MaxMillisecondsPerUpdateStage
	{
		get => maxMillisecondsPerUpdateStage;
		set => maxMillisecondsPerUpdateStage = Math.Clamp(value, 1, 100);
	}
	/// <summary>
	/// Gets or sets the maximum number of jobs that may be executed on the main thread, per update/draw stage.
	/// Limiting time spent on jobs through their number can be a good way to ensure they are leisurely worked off in the background
	/// and without impacting performance in a noticeable way. Jobs are not meant to be high-priority tasks, therefore a low number
	/// should be perfectly fine.
	/// </summary>
	public int MaxJobsPerUpdateStage
	{
		get => maxJobsPerUpdateStage;
		set => maxJobsPerUpdateStage = Math.Clamp(value, 1, 500);
	}

	public Engine Engine { get; init; }

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

		stopwatch.Stop();

		ClearAllJobs(false);

		cancellationTokenSrc?.Dispose();
	}

	/// <summary>
	/// Abort and clear out all pending jobs.
	/// </summary>
	/// <param name="_executeEndedCallbacks">Whether to allow jobs to trigger their end callback as they are aborted.</param>
	public void ClearAllJobs(bool _executeEndedCallbacks = false)
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

	/// <summary>
	/// Queues up a new job for execution in the near-ish future.
	/// </summary>
	/// <param name="_funcJobAction">An action that contains the job's workload.</param>
	/// <param name="_funcJobEndedCallback">A callback that should be invoked when the job has ended. This is called either if the job'e execution completes, or
	/// if the job was aborted.</param>
	/// <param name="_schedule">A schedule type, describing at what time the job may be executed.
	/// If the job is scheduled on a worker thread or on its own thread, the job's action must be thread-safe, as it will not be executed on the main thread.</param>
	/// <param name="_priority">A priority rating for the job's scheduling. Higher priority jobs are pushed ahead in the queue. Keep in mind though, that jobs are
	/// intended to be low-priority tasks that can be set aside for a few update cycles; using complex priotization schemes is rarely a good idea.</param>
	/// <returns>True if the job was queued up for execution, false otherwise.</returns>
	public bool AddJob(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback = null, JobScheduleType _schedule = JobScheduleType.MainThread_PreUpdate, uint _priority = 0u)
	{
		if (_funcJobAction is null)
		{
			Engine.Logger.LogError("Cannot schedule job using null action delegate!");
			return false;
		}

		// Ensure a cancellation source is ready to terminate threaded operations if necessary:
		cancellationTokenSrc ??= new();

		// A) Launch job in its own new thread:
		if (_schedule == JobScheduleType.NewThread)
		{
			ThreadedJob newJob = new(_funcJobAction, _funcJobEndedCallback, OnJobStatusChanged, cancellationTokenSrc.Token)
			{
				Schedule = _schedule,
				Priority = _priority,
			};
			lock (customThreadLockObj)
			{
				jobsInCustomThreads.Add(newJob);
			}
			newJob.Run();
		}
		// B) Enqueue job for execution on main or worker thread:
		else
		{
			BasicJob newJob = new(_funcJobAction, _funcJobEndedCallback, OnJobStatusChanged)
			{
				Schedule = _schedule,
				Priority = _priority,
			};
			EnqueueJob(newJob);
		}
		return true;
	}

	/// <summary>
	/// Queues up a new iterative job for execution in the near-ish future. This job will be executed in its own thread.
	/// </summary>
	/// <param name="_funcIterativeJobAction">An action that contains the job's workload. The action is completed over several steps,
	/// and the progress is reflected by the <see cref="float"/> value yielded at each step, progressing from 0 to 1 as the task nears
	/// completion. If the task encounters an error and must abort early, a negative progress value should be yielded instead.</param>
	/// <param name="_funcJobEndedCallback">A callback that should be invoked when the job has ended. This is called either if the job'e execution completes, or
	/// if the job was aborted.</param>
	/// <param name="_outProgress">Outputs a progress tracker instance for the caller to monitor the job's gradual completion.</param>
	/// <returns>True if the job has started execution in its own thread, false otherwise.</returns>
	public bool AddJob(FuncIterativeJobAction _funcIterativeJobAction, out Progress? _outProgress, FuncJobEndedCallback? _funcJobEndedCallback = null)
	{
		if (_funcIterativeJobAction is null)
		{
			Engine.Logger.LogError("Cannot schedule job using null action delegate!");
			_outProgress = null;
			return false;
		}

		// Ensure a cancellation source is ready to terminate threaded operations if necessary:
		cancellationTokenSrc ??= new();

		_outProgress = new("Job Progress", 100);

		// Launch job in its own new thread:
		ThreadedJob newJob = new(_funcIterativeJobAction, _funcJobEndedCallback, OnJobStatusChanged, _outProgress, cancellationTokenSrc.Token)
		{
			Schedule = JobScheduleType.NewThread,
			Priority = 0u,
		};

		lock (customThreadLockObj)
		{
			jobsInCustomThreads.Add(newJob);
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

				if (workerThread is null || !workerThread.IsAlive)
				{
					workerThread = new(RunWorkerThread);
					workerThread.Start();
				}
			}
		}
	}

	private void RunWorkerThread()
	{
		if (IsDisposed)
		{
			return;
		}
		if (cancellationTokenSrc is not null && cancellationTokenSrc.IsCancellationRequested)
		{
			return;
		}

		cancellationTokenSrc ??= new();

		while (!IsDisposed && !cancellationTokenSrc.IsCancellationRequested)
		{
			if (workerThreadJobs.Count != 0)
			{
				// Dequeue first job in the list:
				lock(workerThreadLockObj)
				{
					currentWorkerThreadJob = workerThreadJobs[0];
					workerThreadJobs.RemoveAt(0);
				}

				// Execute job unless it was previously aborted:
				if (!currentWorkerThreadJob.IsError)
				{
					currentWorkerThreadJob.Run();
					Thread.Sleep(1);
				}
			}
			else
			{
				// No jobs queued up? Sleep until there are:
				Thread.Sleep(16);
			}
		}
	}

	/// <summary>
	/// Work off some of the jobs that have been scheduled for update/draw stages on the main thread. Does nothing if no jobs are queued up for the given schedule type.
	/// </summary>
	/// <param name="_schedule">A main thread schedule type, for which queued-up jobs shall be executed.</param>
	/// <returns>True if jobs were executed successfully or if the queue was empty, false if an error arose.</returns>
	internal bool ProcessJobsOnMainThread(JobScheduleType _schedule)
	{
		if (!_schedule.IsScheduledOnMainThread())
		{
			Engine.Logger.LogError($"Job queue for schedule '{_schedule}' cannot be processed; only jobs scheduled on main thread can be triggered directly!");
			return false;
		}

		// Check if there are any jobs queued up for the current schedule:
		List<Job>? queue;
		int queueCount;
		lock (mainThreadLockObj)
		{
			if (!mainThreadJobQueues.TryGetValue(_schedule, out queue) || queue.Count == 0)
			{
				return true;
			}
			queueCount = queue.Count;
		}

		cancellationTokenSrc ??= new();

		// Execute as many jobs as possible during the alloted limits:
		long batchStartTimeMs = stopwatch.ElapsedMilliseconds;
		int batchJobCount = 0;

		while (
			!cancellationTokenSrc.IsCancellationRequested &&
			batchJobCount < queueCount &&
			stopwatch.ElapsedMilliseconds - batchStartTimeMs < maxMillisecondsPerUpdateStage &&
			batchJobCount < maxJobsPerUpdateStage)
		{
			Job job;
			lock (mainThreadLockObj)
			{
				job = queue[batchJobCount++];
			}
			if (!job.IsError)
			{
				job.Run();
			}
		}

		// Dequeue all jobs that were just completed:
		if (batchJobCount != 0)
		{
			lock (mainThreadLockObj)
			{
				queue.RemoveRange(0, batchJobCount);
			}
		}
		return true;
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
