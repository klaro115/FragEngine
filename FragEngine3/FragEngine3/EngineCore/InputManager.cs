using System.Diagnostics;
using System.Numerics;
using Veldrid;

namespace FragEngine3.EngineCore
{
	public sealed class InputManager : IEngineSystem
	{
		#region Types

		public enum Axis : int
		{
			WASD		= 0,
			IJKL,
			ArrowKeys,
		}

		private struct ButtonState()
		{
			public bool IsDown { get; private set; } = false;
			public bool WasDown { get; private set; } = false;

			public readonly bool IsClicked => IsDown && !WasDown;
			public readonly bool IsReleased => !IsDown && WasDown;

			public void Update(bool _newIsDown)
			{
				WasDown = IsDown;
				IsDown = _newIsDown;
			}
		}

		private struct KeyAxes(Key _xNegative, Key _yNegative, Key _zNegative, Key _xPositive, Key _yPositive, Key _zPositive)
		{
			private readonly Key xNegative = _xNegative;
			private readonly Key yNegative = _yNegative;
			private readonly Key zNegative = _zNegative;

			private readonly Key xPositive = _xPositive;
			private readonly Key yPositive = _yPositive;
			private readonly Key zPositive = _zPositive;

			public Vector3 Value { get; private set; } = Vector3.Zero;
			public Vector3 PrevValue { get; private set; } = Vector3.Zero;
			public Vector3 SmoothedValue { get; private set; } = Vector3.Zero;
			public readonly Vector3 ValueDiff => Value - PrevValue;

			public void Update(in ButtonState[] _keyStates, float _smoothingDiff)
			{
				PrevValue = Value;
				Value = new(
					GetAxis(in _keyStates, xNegative, xPositive),
					GetAxis(in _keyStates, yNegative, yPositive),
					GetAxis(in _keyStates, zNegative, zPositive));
				SmoothedValue = VectorExt.MoveTowards(SmoothedValue, Value, _smoothingDiff);
			}

			private static float GetAxis(in ButtonState[] _keyStates, Key _keyNegative, Key _keyPositive)
			{
				float x = 0.0f;
				if (_keyStates[(int)_keyNegative].IsDown) x -= 1;
				if (_keyStates[(int)_keyPositive].IsDown) x += 1;
				return x;
			}
		}

		#endregion
		#region Constructors

		public InputManager(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

			mouseButtonStates = new ButtonState[14];
			for (int i = 0; i < mouseButtonStates.Length; i++)
			{
				mouseButtonStates[i] = new();
			}

			keyStates = new ButtonState[(int)Key.LastKey];
			for (int i = 0; i < keyStates.Length; i++)
			{
				keyStates[i] = new();
			}

			for (int i = 0; i < keyAxes.Length; ++i)
			{
				keyAxes[i].Update(in keyStates, 1.0f);
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

		private readonly ButtonState[] mouseButtonStates;
		private readonly ButtonState[] keyStates;

		private readonly KeyAxes[] keyAxes =
		[
			new (Key.A, Key.S, Key.Q, Key.D, Key.W, Key.E),
			new (Key.J, Key.K, Key.U, Key.L, Key.I, Key.O),
			new(Key.Left, Key.Down, Key.PageDown, Key.Right, Key.Up, Key.PageUp),
		];
		private float keyAxisSmoothing = 6.0f;

		private readonly Stopwatch stopwatch;
		private long lastInputStateUpdateTimeMs = 0;

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public Engine Engine => engine;

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
		public float MouseWheel { get; private set; } = 0.0f;

		public float KeyAxisSmoothing
		{
			get => keyAxisSmoothing;
			set => keyAxisSmoothing = Math.Clamp(value, 0.001f, 1000.0f);
		}

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _)
		{
			IsDisposed = true;

			stopwatch.Stop();
		}

		public bool GetMouseButton(MouseButton _mouseButton) => mouseButtonStates[(int)_mouseButton].IsDown;
		public bool GetMouseButtonDown(MouseButton _mouseButton) => mouseButtonStates[(int)_mouseButton].IsClicked;
		public bool GetMouseButtonUp(MouseButton _mouseButton) => mouseButtonStates[(int)_mouseButton].IsReleased;

		public bool GetKey(Key _key) => keyStates[(int)_key].IsDown;
		public bool GetKeyDown(Key _key) => keyStates[(int)_key].IsClicked;
		public bool GetKeyUp(Key _key) => keyStates[(int)_key].IsReleased;

		public Vector3 GetKeyAxes(Axis _axis) => keyAxes[(int)_axis].Value;
		public Vector3 GetKeyAxesSmoothed(Axis _axis) => keyAxes[(int)_axis].SmoothedValue;
		public Vector3 GetKeyAxesDiff(Axis _axis) => keyAxes[(int)_axis].ValueDiff;

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
			MouseWheel = _snapshot.WheelDelta;

			// Update keyboard key states and events:
			foreach (KeyEvent keyEvent in _snapshot.KeyEvents)
			{
				int keyIdx = (int)keyEvent.Key;
				keyStates[keyIdx].Update(keyEvent.Down);
			}

			// Update keyboard input axes: (ex.: WASD)
			float axisSmoothingDiff = keyAxisSmoothing * (float)Engine.TimeManager.DeltaTime.TotalSeconds;
			for (int i = 0; i < keyAxes.Length; ++i)
			{
				keyAxes[i].Update(in keyStates, axisSmoothingDiff);
			}

			lastInputStateUpdateTimeMs = newInputStateUpdateTimeMs;
			return true;
		}

		#endregion
	}
}

