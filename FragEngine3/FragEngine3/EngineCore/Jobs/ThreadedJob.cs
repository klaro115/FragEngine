namespace FragEngine3.EngineCore.Jobs;

internal sealed class ThreadedJob(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged, CancellationToken _cancellationToken)
	: Job(_funcJobAction, _funcJobEndedCallback, _funcStatusChanged), IDisposable
{
	#region Constructors

	~ThreadedJob()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly CancellationToken cancellationToken = _cancellationToken;
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
		throw new NotImplementedException();		//TODO: Implement abort system using cancellation token!
	}

	protected override void Run_Impl()
	{
		if (IsDisposed || IsDone) return;

		thread = new(() =>
		{
			IsDone = false;
			IsError = !funcJobAction();
			IsDone = true;
		});
		thread.Start();
	}
	
	#endregion
}
