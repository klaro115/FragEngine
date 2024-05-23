using System.Diagnostics;
using System.Numerics;
using Veldrid;

namespace FragEngine3.EngineCore.Input;

public sealed class InputManager : IEngineSystem
{
	#region Constructors

	public InputManager(Engine _engine)
	{
		engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

		mouseButtonStates = new InputButtonState[14];
		for (int i = 0; i < mouseButtonStates.Length; i++)
		{
			mouseButtonStates[i] = new();
		}

		keyStates = new InputButtonState[(int)Key.LastKey];
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

	private readonly InputButtonState[] mouseButtonStates;
	private readonly InputButtonState[] keyStates;

	private readonly InputKeyAxes[] keyAxes =
	[
		new(Key.A, Key.S, Key.Q, Key.D, Key.W, Key.E),
		new(Key.J, Key.K, Key.U, Key.L, Key.I, Key.O),
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

	/// <summary>
	/// Gets or sets a smoothing time for transitioning from a previous input axes value to the current raw input value.
	/// This value is given in how many seconds it takes to go from an input value of 0 (no pressed) to 1 (fully pressed).
	/// </summary>
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

	public Vector3 GetKeyAxes(InputAxis _axis) => keyAxes[(int)_axis].Value;
	public Vector3 GetKeyAxesSmoothed(InputAxis _axis) => keyAxes[(int)_axis].SmoothedValue;
	public Vector3 GetKeyAxesDiff(InputAxis _axis) => keyAxes[(int)_axis].ValueDiff;

	/// <summary>
	/// Update all input states for the current frame.<para/>
	/// NOTE: This should only ever be called from the main window's message loop via '<see cref="Graphics.GraphicsCore.UpdateMessageLoop(out bool)"/>'.
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
		if (_snapshot is null)
		{
			engine.Logger.LogError("Cannot update input states from null snapshot!");
			return false;
		}

		long newInputStateUpdateTimeMs = stopwatch.ElapsedMilliseconds;
		long inputDeltatimeMs = newInputStateUpdateTimeMs - lastInputStateUpdateTimeMs;

		// Reset event states on idle mouse buttons:
		for (int i = 0; i < mouseButtonStates.Length; ++i)
		{
			ref InputButtonState mouseButtonState = ref mouseButtonStates[i];
			if (mouseButtonState.WasDown != mouseButtonState.IsDown)
			{
				mouseButtonStates[i].Update(mouseButtonState.IsDown);
			}
		}
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

		// Reset event states on idle keys:
		for (int i = 0; i < keyStates.Length; ++i)
		{
			ref InputButtonState keyState = ref keyStates[i];
			if (keyState.WasDown != keyState.IsDown)
			{
				keyStates[i].Update(keyState.IsDown);
			}
		}
		// Update keyboard key states and events:
		foreach (KeyEvent keyEvent in _snapshot.KeyEvents)
		{
			int keyIdx = (int)keyEvent.Key;
			keyStates[keyIdx].Update(keyEvent.Down);
		}


		// Update keyboard input axes: (ex.: WASD)
		float axisSmoothingDiff = Math.Clamp(keyAxisSmoothing * (inputDeltatimeMs * 1000), 0.0f, 1.0f);
		for (int i = 0; i < keyAxes.Length; ++i)
		{
			keyAxes[i].Update(in keyStates, axisSmoothingDiff);
		}

		lastInputStateUpdateTimeMs = newInputStateUpdateTimeMs;
		return true;
	}

	#endregion
}

