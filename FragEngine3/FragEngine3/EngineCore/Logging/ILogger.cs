namespace FragEngine3.EngineCore.Logging;

/// <summary>
/// Interface wrapper around the engine's main logger.
/// This interface exists mostly for mocking functionality, and for using engine components outside of the engine's confines.
/// </summary>
public interface ILogger
{
	#region Properties

	/// <summary>
	/// Gets whether this logger is fully initialized and ready to record or print log messages.
	/// </summary>
	bool IsInitialized { get; }

	#endregion
	#region Methods

	/// <summary>
	/// Logs a regular message.
	/// </summary>
	/// <param name="_message">A message text that describes the event you wish to log.</param>
	/// <param name="_dontPrintToConsole">Whether to ommit printing this message to the console.
	/// If true, the message should only be written to log file, but not shown to the user's console feed.</param>
	public void LogMessage(string _message, bool _dontPrintToConsole = false);

	/// <summary>
	/// Logs a warning message.
	/// </summary>
	/// <param name="_message">A message text that describes the warning.</param>
	void LogWarning(string _message);

	/// <summary>
	/// Logs an error message.
	/// </summary>
	/// <param name="_message">A message text that describes the error.</param>
	void LogError(string _message);

	/// <summary>
	/// Logs an exception message.
	/// </summary>
	/// <param name="_message">A message text that describes the exception's context.</param>
	/// <param name="_exception">An exception that was caught and prompted this message.</param>
	void LogException(string _message, Exception _exception);

	#endregion
}
