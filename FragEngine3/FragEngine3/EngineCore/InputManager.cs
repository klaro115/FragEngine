using Veldrid;

namespace FragEngine3.EngineCore
{
	public sealed class InputManager : IDisposable
	{
		#region Constructors

		public InputManager(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		}
		~InputManager()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine;

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
		private void Dispose(bool _disposing)
		{
			IsDisposed = true;

			//...
		}

		/// <summary>
		/// Update all input states for the current frame.<para/>
		/// NOTE: This should only ever be called from the main window's message loop via '<see cref="Graphics.IGraphicsDevices.UpdateMessageLoop(out bool)"/>'.
		/// </summary>
		/// <param name="_snapshot">A snapshot of the current input events and states, as recorded by the main window procedure..</param>
		/// <returns>True if states were updated, false if snapshot was null or an error occurred.</returns>
		internal bool UpdateInputStates(InputSnapshot _snapshot)
		{
			if (IsDisposed)
			{
				engine.Logger.LogError("Cannot update input states on disposed input manager!");
				return false;
			}
			if (_snapshot == null)
			{
				engine.Logger.LogError("Cannot update input states from null snapshot!");
				return false;
			}

			//TODO: Store and process input states.

			return true;
		}

		#endregion
	}
}

