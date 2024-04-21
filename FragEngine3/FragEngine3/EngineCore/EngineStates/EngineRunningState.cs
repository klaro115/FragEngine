namespace FragEngine3.EngineCore.EngineStates;

internal sealed class EngineRunningState(Engine _engine, ApplicationLogic _applicationLogic) : EngineStateBase(_engine, _applicationLogic)
{
	#region Properties

	protected override EngineState State => EngineState.Running;

	#endregion
	#region Methods

	protected override bool BeginState_internal(bool _verbose)
	{
		if (IsDisposed)
		{
			engine.Logger?.LogError("Cannot begin disposed engine loading state!");
			return false;
		}

		// If requested, log state change:
		if (_verbose)
		{
			engine.Logger.LogStatus("### LOADING CONTENT...");
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
			engine.Logger?.LogError("Cannot update disposed engine loading state!");
			return false;
		}

		return RunMainLoop();
	}

	#endregion
}
