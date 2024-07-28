namespace FragEngine3.EngineCore.Jobs;

internal sealed class BasicJob : Job
{
	#region Constructors

	public BasicJob(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged)
		: base(_funcJobAction, _funcJobEndedCallback, _funcStatusChanged)
	{
	}

	public BasicJob(FuncIterativeJobAction _funcIterativeJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged, CancellationToken _cancellationToken)
		: base(_funcIterativeJobAction, _funcJobEndedCallback, _funcStatusChanged, _cancellationToken)
	{
	}

	#endregion
	#region Methods

	protected override void Abort_Impl()
	{
		if (IsDone) return;

		IsError = true;
		IsDone = false;
	}

	protected override void Run_Impl()
	{
		// Normal job actions are executed in one go:
		if (funcJobAction is not null)
		{
			IsError = !funcJobAction();
			IsDone = true;
		}
		// Iterative actions are looped over until done:
		else
		{
			float progressValue = 0.0f;
			IEnumerator<float> e = funcIterativeJobAction!(new());

			while (
				e.MoveNext() &&
				(progressValue = e.Current) > 0)
			{ }

			IsError = progressValue < 0;
			IsDone = progressValue >= 1;
		}
	}
	
	#endregion
}
