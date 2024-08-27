namespace FragEngine3.EngineCore.EngineStates;

internal sealed class EngineStartupState(Engine _engine, ApplicationLogic _applicationLogic) : EngineStateBase(_engine, _applicationLogic)
{
	#region Properties

	protected override EngineState State => EngineState.Startup;

	#endregion
	#region Methods

	protected override bool BeginState_internal(bool _verbose)
	{
		if (IsDisposed)
		{
			engine.Logger?.LogError("Cannot begin disposed engine start-up state!");
			return false;
		}

		// If requested, log state change:
		if (_verbose)
		{
			engine.Logger.LogStatus("### STARTING UP...");
		}

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
			engine.Logger?.LogError("Cannot update disposed engine start-up state!");
			return false;
		}

		return true;
	}

	#endregion
}
