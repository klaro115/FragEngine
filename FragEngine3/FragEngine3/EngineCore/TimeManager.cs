using System.Diagnostics;

namespace FragEngine3.EngineCore
{
	public sealed class TimeManager : IDisposable
	{
		#region Constructors

		public TimeManager(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(engine), "Engine may not be null!");

			stopwatch = new();
			stopwatch.Start();
		}

		~TimeManager()
		{
			Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine;

		private readonly Stopwatch stopwatch;

		#endregion
		#region Properties

		public long LastFrameStartTimeMs { get; private set; } = 0;
		public long LastFrameEndTimeMs { get; private set; } = 0;
		public long LastFrameDurationMs { get; private set; } = 0;

		public long DeltaTimeMs { get; private set; } = 0;
		public TimeSpan DeltaTime { get; private set; } = TimeSpan.Zero;
		public TimeSpan RunTime { get; private set; } = TimeSpan.Zero;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _)
		{
			stopwatch.Stop();
		}

		public void Reset()
		{
			stopwatch.Restart();
		}

		public bool BeginFrame(out long _outStartTimeMs)
		{
			RunTime = stopwatch.Elapsed;

			_outStartTimeMs = stopwatch.ElapsedMilliseconds;
			LastFrameStartTimeMs = _outStartTimeMs;

			return true;
		}

		public bool EndFrame(out long _outEndTimeMs, out long _outFrameDurationMs)
		{
			TimeSpan newRunTime = stopwatch.Elapsed;
			DeltaTime = newRunTime - RunTime;
			DeltaTimeMs = (long)DeltaTime.TotalMilliseconds;
			RunTime = newRunTime;

			_outEndTimeMs = stopwatch.ElapsedMilliseconds;
			_outFrameDurationMs = DeltaTimeMs;
			LastFrameEndTimeMs = _outEndTimeMs;
			LastFrameDurationMs = LastFrameEndTimeMs - LastFrameStartTimeMs;

			return true;
		}

		#endregion
	}
}
