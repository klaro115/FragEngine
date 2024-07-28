namespace FragEngine3.EngineCore.Jobs;

public abstract class Job
{
	#region Constructors

	internal Job(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged)
	{
		funcJobAction = _funcJobAction ?? throw new ArgumentNullException(nameof(_funcJobAction), "Job action delegate may not be null!");
		funcJobEndedCallback = _funcJobEndedCallback;
		funcStatusChanged = _funcStatusChanged;
	}

	#endregion
	#region Fields

	protected readonly FuncJobAction funcJobAction;
	public readonly FuncJobEndedCallback? funcJobEndedCallback;
	private readonly FuncJobStatusChanged funcStatusChanged;

	#endregion
	#region Properties

	public JobScheduleType Schedule { get; init; } = JobScheduleType.MainThread_MainUpdate;
	public uint Priority { get; init; } = 0;

	public bool IsDone { get; protected set; } = false;
	public bool IsError { get; protected set; } = false;

	#endregion
	#region Methods

	public void Abort(bool _executeEndedCallback)
	{
		Abort_Impl();
		funcStatusChanged(this, _executeEndedCallback);
	}
	protected abstract void Abort_Impl();

	public bool Run()
	{
		Run_Impl();
		funcStatusChanged(this, true);
		return IsDone && !IsError;
	}
	protected abstract void Run_Impl();

	#endregion
}
