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

		private readonly List<LogEntry> entries = new(64);
		private readonly int maxEntryCountBeforeClear = 64;
		private readonly int keepEntryCountAfterClear = 4;
		private readonly int writeLogsEveryNEntries = 1;
		private int entriesKeptAfterClear = 0;

		public readonly string applicationPath;
		public readonly string logDirAbsPath;
		public readonly string logFileAbsPath;

		private static readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsInitialized { get; private set; } = false;

		public static Logger? I { get; private set; } = null;

		#endregion
		#region Methods

		public bool Initialize()
		{
			if (IsInitialized) return true;

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

			if (I == null || !I.IsInitialized)
			{
				I = this;
			}

			// Log the moment the log file was created and written to:
			if (createdNew)
			{
				LogMessage("Log file created.");
			}

			LogMessage("-----------------------------------------------");
			LogMessage("Logging session started.");

			IsInitialized = true;
			return true;
		}

		public void Shutdown()
		{
			LogMessage("Logging session ended.");
			LogMessage("-----------------------------------------------");

			// Write all pending entries to file:
			int pendingEntryCount = Math.Max(entries.Count - entriesKeptAfterClear, 0);
			WriteLogs(entriesKeptAfterClear, pendingEntryCount);

			if (I == this)
			{
				I = null;
			}

			IsInitialized = false;
		}

		public void LogMessage(string _message)
		{
			LogEntry entry = new(LogEntryType.Message, _message);
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

		public void LogNewEntry(LogEntry _entry)
		{
			entries.Add(_entry);

			// Once enough new 
			if (entries.Count - keepEntryCountAfterClear >= writeLogsEveryNEntries)
			{
				int excessCount = Math.Max(entries.Count - maxEntryCountBeforeClear, 0);
				int firstNewEntryIdx = Math.Clamp(entriesKeptAfterClear - 1, 0, entries.Count);
				
				WriteLogs(firstNewEntryIdx, excessCount);
				entries.RemoveRange(0, excessCount);
			}

			// Clear list of log entries after a certain number has been logged:
			if (entries.Count > maxEntryCountBeforeClear)
			{
				int excessCount = Math.Max(entries.Count - keepEntryCountAfterClear, 0);
				entriesKeptAfterClear = Math.Max(entries.Count - excessCount, 0);

				entries.RemoveRange(0, excessCount);
			}

			// Write the log entry to console for instant user debugging:
			Console.WriteLine(_entry.ToString());
		}

		private bool WriteLogs(int _startIdx, int _count)
		{
			if (_count == 0) return true;

			lock(lockObj)
			{
				FileStream? stream = null;
				TextWriter? writer = null;
				try
				{
					stream = File.Open(logFileAbsPath, FileMode.Append, FileAccess.Write);
					writer = new StreamWriter(stream);

					int endIdx = Math.Min(_startIdx + _count, entries.Count);
					for (int i = _startIdx; i < endIdx; ++i)
					{
						LogEntry entry = entries[i];
						writer.WriteLine(entry.ToString());
					}
				}
				finally
				{
					writer?.Dispose();
					stream?.Close();
				}
			}

			return true;
		}

		#endregion
	}
}

