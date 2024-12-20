namespace FragEngine3.EngineCore.EngineStates;

internal sealed class EngineShutdownState(Engine _engine, ApplicationLogic _applicationLogic) : EngineStateBase(_engine, _applicationLogic)
{
	#region Properties

	protected override EngineState State => EngineState.Shutdown;

	#endregion
	#region Methods

	protected override bool BeginState_internal(bool _verbose)
	{
		if (IsDisposed)
		{
			engine.Logger?.LogError("Cannot begin disposed engine shutdown state!");
			return false;
		}

		// If requested, log state change:
		if (_verbose)
		{
			engine.Logger.LogStatus("### SHUTTING DOWN...");
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
			engine.Logger?.LogError("Cannot update disposed engine shutdown state!");
			return false;
		}

		engine.HealthCheckSystem.RemoveAllChecks();
		return true;
	}

	#endregion
}
