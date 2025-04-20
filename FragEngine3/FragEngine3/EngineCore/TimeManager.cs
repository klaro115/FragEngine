using System.Diagnostics;

namespace FragEngine3.EngineCore;

/// <summary>
/// The central time management service of the engine.
/// This class provides timers, and calculates delta time and thread sleep durations for the main loop.
/// </summary>
public sealed class TimeManager : IEngineSystem
{
	#region Types

	/// <summary>
	/// Reset modes that dictate if and how long to wait befire resetting <see cref="ShaderTime"/>.
	/// A delayed reset mode can be used to prevent visual artifacting in timer-based visual effects when the time value is reset to zero.
	/// </summary>
	public enum ShaderTimeResetMode
	{
		/// <summary>
		/// Don't wait, reset immediately.
		/// </summary>
		None,

		/// <summary>
		/// Wait for the current minute of <see cref="RunTime"/> to end.
		/// </summary>
		WaitForEndOfMinute,
		/// <summary>
		/// Wait for the current hour of <see cref="RunTime"/> to end.
		/// </summary>
		WaitForEndOfHour,

		/// <summary>
		/// Wait until the sine function of time crosses zero. <code>sin(t) == 0</code>
		/// </summary>
		WaitForSinZero,
		/// <summary>
		/// Wait until the cosine function of time crosses zero. <code>cos(t) == 0</code>
		/// </summary>
		WaitForCosZero,
	}

	#endregion
	#region Constructors

	public TimeManager(Engine _engine)
	{
		Engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

		EngineStartupDateTimeUtc = DateTime.UtcNow;
		EngineStateChangeDateTimeUtc = DateTime.UtcNow;
		Engine.OnStateChanged += OnEngineStateChanged;

		stopwatch = new();
		stopwatch.Start();
	}

	~TimeManager()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	private readonly Stopwatch stopwatch;

	private TimeSpan targetFrameDuration = new(0, 0, 0, 0, 16, 667);
	private double targetFrameRate = 60.0;

	private bool isShaderTimeResetPending = false;
	private TimeSpan shaderTimeStartOffset = TimeSpan.Zero;
	private TimeSpan shaderTimeResetTarget = TimeSpan.Zero;

	#endregion
	#region Constants

	private static readonly TimeSpan minFrameDuration = new(0, 0, 0, 0, 1);
	private static readonly TimeSpan maxFrameDuration = new(0, 0, 0, 1, 0);

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Engine Engine { get; }

	/// <summary>
	/// Gets the date and time when the engine was started up, in UTC.
	/// </summary>
	public DateTime EngineStartupDateTimeUtc { get; }
	/// <summary>
	/// Gets the date and time of the last time the engine's state changed, in UTC.
	/// </summary>
	public DateTime EngineStateChangeDateTimeUtc { get; private set; }

	public TimeSpan LastFrameStartTime { get; private set; } = TimeSpan.Zero;
	public TimeSpan LastFrameEndTime { get; private set; } = TimeSpan.Zero;
	public TimeSpan LastFrameDuration { get; private set; } = TimeSpan.Zero;
	public long LastFrameDurationMs => LastFrameDuration.Milliseconds;

	public TimeSpan DeltaTime { get; private set; } = TimeSpan.Zero;
	public long DeltaTimeMs => DeltaTime.Milliseconds;

	public TimeSpan RunTime { get; private set; } = TimeSpan.Zero;
	public long FrameCount { get; private set; } = 0;

	public TimeSpan ShaderTime { get; private set; } = TimeSpan.Zero;
	public float ShaderTimeSeconds { get; private set; } = 0.0f;

	/// <summary>
	/// Gets or sets the targeted frame duration of the engine's main loop. The program will try to lock
	/// the rate at which its main thread recalculates the application logic to this time limit. Must be
	/// a value in the range between 1 millisecond and 1 second.
	/// </summary>
	public TimeSpan TargetFrameDuration
	{
		get => targetFrameDuration;
		set
		{
			if (value > maxFrameDuration) value = maxFrameDuration;
			else if (value < minFrameDuration) value = minFrameDuration;
			targetFrameDuration = value;
			targetFrameRate = 1000.0 / targetFrameDuration.TotalMilliseconds;
		}
	}
	/// <summary>
	/// Gets or sets the targeted frame rate of the engine's main loop. The program will try to lock the
	/// rate at which its main thread recalculates the application logic to this frequency. Must be a
	/// value in the range between 1 Hz and 1000 Hz.
	/// </summary>
	public double TargetFrameRate
	{
		get => targetFrameRate;
		set
		{
			targetFrameRate = Math.Clamp(value, 1.0, 1000.0);
			targetFrameDuration = TimeSpan.FromMilliseconds(1000.0 / targetFrameRate);
		}
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

	public void Reset()
	{
		stopwatch.Restart();

		RunTime = TimeSpan.Zero;

		LastFrameStartTime = TimeSpan.Zero;
		LastFrameEndTime = TimeSpan.Zero;
		LastFrameDuration = TimeSpan.Zero;

		DeltaTime = targetFrameDuration;
	}

	private void OnEngineStateChanged(EngineState _)
	{
		EngineStateChangeDateTimeUtc = DateTime.UtcNow;
	}

	internal bool BeginFrame()
	{
		if (IsDisposed)
		{
			Engine.Logger.LogError("Cannot begin frame using disposed time manager!");
			return false;
		}

		RunTime = stopwatch.Elapsed;
		FrameCount++;

		LastFrameStartTime = stopwatch.Elapsed;

		return true;
	}

	internal bool EndFrame(out TimeSpan _outThreadSleepTime)
	{
		if (IsDisposed)
		{
			Engine.Logger.LogError("Cannot end frame using disposed time manager!");
			_outThreadSleepTime = TimeSpan.Zero;
			return false;
		}

		// Update engine running time:
		TimeSpan newRunTime = stopwatch.Elapsed;
		RunTime = newRunTime;

		// Update shader time:
		ShaderTime = RunTime - shaderTimeStartOffset;
		ShaderTimeSeconds = (float)ShaderTime.TotalSeconds;
		if (isShaderTimeResetPending && RunTime >= shaderTimeResetTarget)
		{
			ResetShaderTime_internal();
		}

		// Update delta time & sleep durations:
		LastFrameEndTime = stopwatch.Elapsed;
		LastFrameDuration = LastFrameEndTime - LastFrameStartTime;

		if (targetFrameDuration > LastFrameDuration)
		{
			_outThreadSleepTime = targetFrameDuration - LastFrameDuration;
			DeltaTime = targetFrameDuration;
		}
		else
		{
			_outThreadSleepTime = TimeSpan.Zero;
			DeltaTime = LastFrameDuration;
		}
		return true;
	}

	/// <summary>
	/// Request a reset of the <see cref="ShaderTime"/> to zero.<para/>
	/// Note: Shaders receive time stamps only as a 32-bit floating number, with inaccuracy increasing as time passes.
	/// This is fine for delta times, but not ideal for a continuous timer. You may therefore use this method to reset
	/// the timer to zero, to prevent rounding errors and visual artifacting in long-running games whose shaders rely
	/// on an high-resolution timer value.
	/// </summary>
	/// <param name="_resetMode">Enum value spacifying conditions for delaying the timer reset. An immediate reset may
	/// lead to noticeable jumps/cuts in visual effects between frames; waiting for the timer to cross a threshold, or
	/// for a function of time to cross zero, can mitigate this.</param>
	/// <returns>True if the timer reset was scheduled or enacted, false otherwise.</returns>
	public bool ResetShaderTime(ShaderTimeResetMode _resetMode = ShaderTimeResetMode.None)
	{
		if (IsDisposed)
		{
			Engine.Logger.LogError("Cannot reset shader time of disposed time manager!");
			return false;
		}

		const double sinTimePeriodMs = Math.PI * 1000.0;

		TimeSpan curTime = RunTime;
		isShaderTimeResetPending = true;

		switch (_resetMode)
		{
			// Immediate reset:
			case ShaderTimeResetMode.None:
				{
					ResetShaderTime_internal();
				}
				return true;

			// Unit of time:
			case ShaderTimeResetMode.WaitForEndOfMinute:
				{
					shaderTimeResetTarget = new TimeSpan(curTime.Days, curTime.Hours, curTime.Minutes, 0) + TimeSpan.FromMinutes(1);
				}
				return true;
			case ShaderTimeResetMode.WaitForEndOfHour:
				{
					shaderTimeResetTarget = new TimeSpan(curTime.Days, curTime.Hours, 0, 0) + TimeSpan.FromHours(1);
				}
				return true;

			// Function of time:
			case ShaderTimeResetMode.WaitForSinZero:
				{
					double nextHalfPeriodIndex = Math.Ceiling(curTime.TotalMilliseconds / sinTimePeriodMs);
					double nextHalfPeriodMilliseconds = nextHalfPeriodIndex * sinTimePeriodMs;

					shaderTimeResetTarget = TimeSpan.FromMilliseconds(nextHalfPeriodMilliseconds);
				}
				return true;
			case ShaderTimeResetMode.WaitForCosZero:
				{
					double nextHalfPeriodIndex = Math.Ceiling(curTime.TotalMilliseconds / sinTimePeriodMs + 0.5);
					double nextHalfPeriodMilliseconds = nextHalfPeriodIndex * sinTimePeriodMs;

					shaderTimeResetTarget = TimeSpan.FromMilliseconds(nextHalfPeriodMilliseconds);
				}
				return true;

			// Misc:
			default:
				{
					isShaderTimeResetPending = false;
				}
				return false;
		}
	}

	private void ResetShaderTime_internal()
	{
		isShaderTimeResetPending = false;
		shaderTimeStartOffset = RunTime;
		shaderTimeResetTarget = RunTime;

		ShaderTime = TimeSpan.Zero;
		ShaderTimeSeconds = 0.0f;
	}

	#endregion
}
