using System.Diagnostics;

namespace FragEngine3.EngineCore;

public sealed class TimeManager : IEngineSystem
{
	#region Constructors

	public TimeManager(Engine _engine)
	{
		Engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

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

	#endregion
	#region Constants

	private static readonly TimeSpan minFrameDuration = new(0, 0, 0, 0, 1);
	private static readonly TimeSpan maxFrameDuration = new(0, 0, 0, 1, 0);

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Engine Engine { get; }

	public TimeSpan LastFrameStartTime { get; private set; } = TimeSpan.Zero;
	public TimeSpan LastFrameEndTime { get; private set; } = TimeSpan.Zero;
	public TimeSpan LastFrameDuration { get; private set; } = TimeSpan.Zero;
	public long LastFrameDurationMs => LastFrameDuration.Milliseconds;

	public TimeSpan DeltaTime { get; private set; } = TimeSpan.Zero;
	public long DeltaTimeMs => DeltaTime.Milliseconds;

	public TimeSpan RunTime { get; private set; } = TimeSpan.Zero;
	public long FrameCount { get; private set; } = 0;

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

		TimeSpan newRunTime = stopwatch.Elapsed;
		RunTime = newRunTime;

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

	#endregion
}
