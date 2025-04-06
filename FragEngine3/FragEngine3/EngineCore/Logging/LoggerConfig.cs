namespace FragEngine3.EngineCore.Logging;

/// <summary>
/// Startup configuration and settings for the engine's main <see cref="Logger"/> system.
/// </summary>
[Serializable]
public sealed class LoggerConfig
{
	#region Properties

	/// <summary>
	/// Whether to delete any log files that were created in previous sessions as part of the startup process.
	/// </summary>
	public bool DeletePreviousLogsOnStartup { get; init; } = true;

	/// <summary>
	/// The number of logs that are cached in memory before they're written out to log file in one go.
	/// If you want to write to file immediately after each new log entry, set this to 1. May not be zero or negative.
	/// </summary>
	public uint WriteLogsEveryNEntries { get; init; } = 1;

	//...

	#endregion
	#region Methods

	/// <summary>
	/// Creates a deep copy of this config.
	/// </summary>
	public LoggerConfig Clone()
	{
		return new LoggerConfig()
		{
			DeletePreviousLogsOnStartup = DeletePreviousLogsOnStartup,
			WriteLogsEveryNEntries = WriteLogsEveryNEntries,
			//...
		};
	}

	#endregion
}
