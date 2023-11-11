using FragEngine3.EngineCore.Logging;

namespace FragEngine3.EngineCore
{
	public sealed class Logger
	{
		#region Constructors

		public Logger(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

			applicationPath = Path.GetFullPath(Environment.CurrentDirectory);
			logDirAbsPath = Path.Combine(applicationPath, LoggerConstants.LOG_FILE_DIR_REL_PATH);
			logFileAbsPath = Path.Combine(logDirAbsPath, LoggerConstants.MAIN_LOG_FILE_NAME);
		}

		#endregion
		#region Fields

		public readonly Engine engine;

		private readonly Queue<LogEntry> entries = new(64);
		private readonly int writeLogsEveryNEntries = 1;

		public readonly string applicationPath;
		public readonly string logDirAbsPath;
		public readonly string logFileAbsPath;

		private static readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsInitialized { get; private set; } = false;

		public static Logger? Instance { get; private set; } = null;

		#endregion
		#region Methods

		public bool Initialize()
		{
			if (IsInitialized) return true;

			lock(lockObj)
			{
				entries.Clear();
			}

			// Ensure the log file and its parent directory exist:
			bool createdNew = false;
			StreamWriter? writer = null;
			try
			{
				lock(lockObj)
				{
					if (!File.Exists(logFileAbsPath))
					{
						if (!Directory.Exists(logDirAbsPath))
						{
							Directory.CreateDirectory(logDirAbsPath);
						}

						writer = File.CreateText(logFileAbsPath);
						createdNew = true;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to create log file directory and main log file!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				IsInitialized = false;
				return false;
			}
			finally
			{
				writer?.Close();
			}

			lock(lockObj)
			{
				if (Instance == null || !Instance.IsInitialized)
				{
					Instance = this;
				}
			}

			// Log the moment the log file was created and written to:
			if (createdNew)
			{
				LogMessage("Log file created.");
			}

			LogMessage("-----------------------------------------------");
			LogStatus("Logging session started.");

			IsInitialized = true;
			return true;
		}

		public void Shutdown()
		{
			if (!IsInitialized) return;

			LogStatus("Logging session ended.");
			LogMessage("-----------------------------------------------\n\n\n\n");

			// Write all pending entries to file:
			WriteLogs();

			if (Instance == this)
			{
				Instance = null;
			}

			IsInitialized = false;
		}

		public void LogMessage(string _message, bool _dontPrintToConsole = false)
		{
			LogEntry entry = new(LogEntryType.Message, _message);
			LogNewEntry(entry, _dontPrintToConsole);
		}

		public void LogWarning(string _message)
		{
			LogEntry entry = new(LogEntryType.Warning, _message);
			LogNewEntry(entry);
		}

		public void LogError(string _message)
		{
			LogEntry entry = new(LogEntryType.Error, _message, -1, LogEntrySeverity.Normal);
			LogNewEntry(entry);
		}
		public void LogError(string _message, int _errorCode = -1, LogEntrySeverity _severity = LogEntrySeverity.Normal)
		{
			LogEntry entry = new(LogEntryType.Error, _message, _errorCode, _severity);
			LogNewEntry(entry);
		}

		public void LogException(string _message, Exception _exception)
		{
			LogEntry entry = new(LogEntryType.Error, _message, -1, LogEntrySeverity.Major, _exception);
			LogNewEntry(entry);
		}
		public void LogException(string _message, Exception _exception, LogEntrySeverity _severity = LogEntrySeverity.Normal)
		{
			LogEntry entry = new(LogEntryType.Error, _message, -1, _severity, _exception);
			LogNewEntry(entry);
		}

		public void LogStatus(string _message, LogEntrySeverity _severity = LogEntrySeverity.Normal)
		{
			LogEntry entry = new(LogEntryType.Status, _message, 0, _severity);
			LogNewEntry(entry);
		}

		public void LogNewEntry(LogEntry _entry, bool _dontPrintToConsole = false)
		{
			lock(lockObj)
			{
				entries.Enqueue(_entry);
			}

			// Once enough new entries have been queued up, write them to file:
			if (entries.Count >= writeLogsEveryNEntries)
			{
				WriteLogs();
			}

			// Write the log entry to console for instant user debugging:
			if (!_dontPrintToConsole)
			{
				lock (lockObj)
				{
					Console.ForegroundColor = _entry.GetConsoleColor();
					Console.WriteLine(_entry.ToString());
					Console.ForegroundColor = LogEntry.defaultConsoleColor;
				}
			}
		}

		private bool WriteLogs()
		{
			bool success;

			lock(lockObj)
			{
				if (entries.Count == 0) return true;

				FileStream? stream = null;
				TextWriter? writer = null;
				try
				{
					stream = File.Open(logFileAbsPath, FileMode.Append, FileAccess.Write);
					writer = new StreamWriter(stream);

					while (entries.TryDequeue(out LogEntry? entry))
					{
						writer.WriteLine(entry.FormatLogString());
					}
					success = true;
				}
				catch
				{
					success = false;
				}
				finally
				{
					writer?.Dispose();
					stream?.Close();
				}
			}

			return success;
		}

		#endregion
	}
}

