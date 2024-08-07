﻿namespace FragEngine3.EngineCore.Jobs;

internal abstract class Job
{
	#region Constructors

	internal Job(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged)
	{
		funcJobAction = _funcJobAction ?? throw new ArgumentNullException(nameof(_funcJobAction), "Job action delegate may not be null!");
		funcIterativeJobAction = null;

		funcJobEndedCallback = _funcJobEndedCallback;
		funcStatusChanged = _funcStatusChanged;

		cancellationToken = default;
	}

	internal Job(FuncIterativeJobAction _funcIterativeJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged, CancellationToken _cancellationToken)
	{
		funcJobAction = null;
		funcIterativeJobAction = _funcIterativeJobAction ?? throw new ArgumentNullException(nameof(_funcIterativeJobAction), "Job action delegate may not be null!");

		funcJobEndedCallback = _funcJobEndedCallback;
		funcStatusChanged = _funcStatusChanged;

		cancellationToken = _cancellationToken;
	}

	#endregion
	#region Fields

	protected readonly FuncJobAction? funcJobAction;
	protected readonly FuncIterativeJobAction? funcIterativeJobAction;
	protected readonly CancellationToken cancellationToken;

	public readonly FuncJobEndedCallback? funcJobEndedCallback;
	protected readonly FuncJobStatusChanged funcStatusChanged;

	#endregion
	#region Properties

	public JobScheduleType Schedule { get; init; } = JobScheduleType.MainThread_PreUpdate;
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
		if (IsDone)
		{
			funcStatusChanged(this, true);
		}
		return IsDone && !IsError;
	}
	protected abstract void Run_Impl();

	#endregion
}
