using System.Diagnostics;
using System.Numerics;
using Veldrid;

namespace FragEngine3.EngineCore
{
	public sealed class InputManager : IEngineSystem
	{
		#region Types

		private struct MouseButtonState
		{
			public bool isDown;
			public bool wasDown;

			public readonly bool IsClicked => isDown && !wasDown;
			public readonly bool IsReleased => !isDown && wasDown;

			public void Update(bool _newIsDown)
			{
				wasDown = isDown;
				isDown = _newIsDown;
			}
		}

		#endregion
		#region Constructors

		public InputManager(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

			mouseButtonStates = new MouseButtonState[14];
			for (int i = 0; i < mouseButtonStates.Length; i++)
			{
				mouseButtonStates[i] = new() { isDown = false, wasDown = false };
			}

			stopwatch = new();
			stopwatch.Start();
		}

		~InputManager()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine;

		private readonly MouseButtonState[] mouseButtonStates;

		/// <summary>
		/// Current position of the mouse cursor, in pixels.
		/// </summary>
		public Vector2 MousePosition { get; private set; } = new();
		/// <summary>
		/// Difference in positions between last frame and the current one, in pixels.
		/// </summary>
		public Vector2 MouseMovement { get; private set; } = new();
		/// <summary>
		/// Current mouse velocity, in pixels per second.
		/// </summary>
		public Vector2 MouseVelocity { get; private set; } = new();

		private readonly Stopwatch stopwatch;
		private long lastInputStateUpdateTimeMs = 0;

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public Engine Engine => engine;

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

			stopwatch.Stop();
			//...
		}

		public bool GetMouseButton(MouseButton _mouseButton) => mouseButtonStates[(int)_mouseButton].isDown;
		public bool GetMouseButtonDown(MouseButton _mouseButton) => mouseButtonStates[(int)_mouseButton].IsClicked;
		public bool GetMouseButtonUp(MouseButton _mouseButton) => mouseButtonStates[(int)_mouseButton].IsReleased;

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

			long newInputStateUpdateTimeMs  = stopwatch.ElapsedMilliseconds;
			long inputDeltatimeMs = lastInputStateUpdateTimeMs - newInputStateUpdateTimeMs;

			// Update mouse button states and events:
			foreach (MouseEvent mouseEvent in _snapshot.MouseEvents)
			{
				int mouseIdx = (int)mouseEvent.MouseButton;
				mouseButtonStates[mouseIdx].Update(mouseEvent.Down);
			}

			// Update mouse movement positions and velocity:
			MouseMovement = _snapshot.MousePosition - MousePosition;
			MousePosition = _snapshot.MousePosition;
			MouseVelocity = MouseMovement / (1000.0f * inputDeltatimeMs);

			//TODO: Store and process input states.

			lastInputStateUpdateTimeMs = newInputStateUpdateTimeMs;
			return true;
		}

		#endregion
	}
}

