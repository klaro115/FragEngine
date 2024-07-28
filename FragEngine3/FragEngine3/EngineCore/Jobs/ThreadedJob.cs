using FragEngine3.Containers;

namespace FragEngine3.EngineCore.Jobs;

internal sealed class ThreadedJob : Job, IDisposable
{
	#region Constructors

	public ThreadedJob(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged, CancellationToken _cancellationToken)
		: base(_funcJobAction, _funcJobEndedCallback, _funcStatusChanged)
	{
	}

	public ThreadedJob(FuncIterativeJobAction _funcIterativeJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged, Progress? _progress, CancellationToken _cancellationToken)
		: base(_funcIterativeJobAction, _funcJobEndedCallback, _funcStatusChanged, _cancellationToken)
	{
		progress = _progress;
	}

	~ThreadedJob()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private bool isAborted = false;

	private readonly Progress? progress;

	private Thread? thread = null;

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

	private void Dispose(bool disposing)
	{
		IsDisposed = true;

		if (thread is not null && thread.IsAlive)
		{
			Abort_Impl();
		}
	}

	protected override void Abort_Impl()
	{
		isAborted = true;
	}

	protected override void Run_Impl()
	{
		if (IsDisposed || IsDone) return;
		
		// Start thread with different execution behaviour depending on action type:
		thread = funcJobAction is not null
			? new(RunThread)
			: new(RunThreadIterative);
		thread.Start();
	}

	private void RunThread()
	{
		IsDone = false;
		progress?.Update(null, 0, 1);

		// Normal job actions are executed in one go:
		IsError = !funcJobAction!();

		IsDone = true;
		progress?.CompleteAllTasks();
		progress?.Finish();
	}

	private void RunThreadIterative()
	{
		IsDone = false;
		progress?.Update(null, 0, 100);

		float progressValue = 0.0f;
		IEnumerator<float> e = funcIterativeJobAction!(cancellationToken);

		// Iterative actions are looped over until done or aborted:
		while (
			!isAborted &&
			!cancellationToken.IsCancellationRequested &&
			e.MoveNext() &&
			(progressValue = e.Current) > 0)
		{
			progress?.Update(null, (int)(progressValue * 100), 100);
		}

		IsError = progressValue < 0;
		IsDone = progressValue >= 1;
		progress?.Finish();
	}
	
	#endregion
}
