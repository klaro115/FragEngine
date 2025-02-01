namespace FragEngine3.EngineCore.EngineStates;

/// <summary>
/// Structure that contains a brief report on the performance metrics of the engine's states.
/// </summary>
public readonly struct EngineStateReport
{
	#region Fields

	/// <summary>
	/// The state for which this report was generated.
	/// </summary>
	public required EngineState State { get; init; }
	/// <summary>
	/// A UTC-timestamp of the date and time when this report was generated.
	/// </summary>
	public required DateTime ReportTimeUtc { get; init; }

	/// <summary>
	/// The average frame rate, in frames per second.
	/// </summary>
	public required double AvgFrameRate { get; init; }
	/// <summary>
	/// The average duration of one frame, in milliseconds.<para/>
	/// Note: This duration includes any thread sleep time that is added to maintain a stable target frame rate.
	/// </summary>
	public required double AvgFrameTimeMs { get; init; }
	/// <summary>
	/// The average duration of computations per frame, in milliseconds.<para/>
	/// Note: This differs from <see cref="AvgFrameTimeMs"/> insofar as it excludes the thread sleep time needed to reach the desired maximum frame rate.
	/// </summary>
	public required double AvgComputeTimeMs { get; init; }
	/// <summary>
	/// The percentage of frames where frame rates dropped below 40 Hertz.
	/// </summary>
	public required double SlowFramePerc { get; init; }

	#endregion
	#region Properties

	/// <summary>
	/// Gets an invalid report with all-zero values.
	/// </summary>
	public static EngineStateReport Zero => new()
	{
		State = EngineState.None,
		ReportTimeUtc = new(),
		AvgFrameRate = 0,
		AvgFrameTimeMs = 0,
		AvgComputeTimeMs = 0,
		SlowFramePerc = 0,
	};

	#endregion
	#region Methods

	/// <summary>
	/// Logs the contents of the report.
	/// </summary>
	/// <param name="_logger">The logger to use for printing/storing the report message.</param>
	public readonly void LogReport(Logger _logger)
	{
		if (_logger is null || !_logger.IsInitialized)
		{
			return;
		}

		_logger.LogMessage($"Engine state {State} | Average frame rate: {AvgFrameRate:0.00} Hz | Average frame time: {AvgFrameTimeMs:0.00} ms | Average compute time: {AvgComputeTimeMs:0.00} ms | Frames above 25 ms: {SlowFramePerc:0.0}%");
	}

	#endregion
}
