﻿
using FragEngine3.Resources;

namespace FragEngine3.EngineCore
{
    public abstract class ApplicationLogic
	{
		#region Fields

		private Engine? engine = null;

		#endregion
		#region Properties

		public Engine Engine => engine!;

		#endregion
		#region Methods

		internal bool AssignEngine(Engine _engine)
		{
			if (_engine == null || _engine.IsDisposed)
			{
				Console.WriteLine("Error! Cannot assign disposed or null engine to application logic module!");
				return false;
			}
			if (engine != null)
			{
				Console.WriteLine("Error! An engine was already assigned to this application logic module!");
				return false;
			}

			engine = _engine;
			return true;
		}

		public bool SetEngineState(EngineState _prevState, EngineState _newState)
		{
			bool successEnd = _prevState switch
			{
				EngineState.Loading => EndLoadingState(),
				EngineState.Running => EndRunningState(),
				EngineState.Unloading => EndUnloadingState(),
				_ => true,
			};
			if (!successEnd)
			{
				Console.WriteLine($"Error! Failed to execute exit logic for previous engine state '{_prevState}'!");
			}

			bool successBegin = _newState switch
			{
				EngineState.Startup => RunStartupLogic(),
				EngineState.Loading => BeginLoadingState(),
				EngineState.Running => BeginRunningState(),
				EngineState.Unloading => BeginUnloadingState(),
				EngineState.Shutdown => RunShutdownLogic(),
				_ => true,
			};
			if (!successBegin)
			{
				Console.WriteLine($"Error! Failed to execute beginning logic for new engine state '{_newState}'!");
			}

			return successEnd && successBegin;
		}

		protected abstract bool RunStartupLogic();
		protected abstract bool RunShutdownLogic();

		protected abstract bool BeginLoadingState();
		protected abstract bool EndLoadingState();

		protected abstract bool BeginRunningState();
		public abstract bool UpdateRunningState();
		protected abstract bool EndRunningState();

		protected abstract bool BeginUnloadingState();
		protected virtual bool EndUnloadingState() { return true; }

		#endregion
	}
}