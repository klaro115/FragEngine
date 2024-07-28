namespace FragEngine3.EngineCore.Jobs;

internal sealed class BasicJob(FuncJobAction _funcJobAction, FuncJobEndedCallback? _funcJobEndedCallback, FuncJobStatusChanged _funcStatusChanged)
	: Job(_funcJobAction, _funcJobEndedCallback, _funcStatusChanged)
{
	#region Methods

	protected override void Abort_Impl()
	{
		if (IsDone) return;

		IsError = true;
		IsDone = false;
	}

	protected override void Run_Impl()
	{
		IsError = !funcJobAction();
		IsDone = true;
	}
	
	#endregion
}
