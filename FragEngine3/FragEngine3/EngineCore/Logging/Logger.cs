using FragEngine3.EngineCore.Logging;
using FragEngine3.Utility;
using System.Text;
using Veldrid.Sdl2;

namespace FragEngine3.EngineCore;

public sealed class Logger : ILogger, IEngineSystem
{
	#region Constructors

	public Logger(Engine _engine)
	{
		Engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

		string? entryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
		applicationPath = entryPath ?? Environment.CurrentDirectory;
		applicationPath = Path.GetFullPath(applicationPath);
		
		logDirAbsPath = Path.Combine(applicationPath, LoggerConstants.LOG_FILE_DIR_REL_PATH);
		logFileAbsPath = Path.Combine(logDirAbsPath, LoggerConstants.MAIN_LOG_FILE_NAME);
	}

	~Logger()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever a new entry was logged.
	/// </summary>
	public event Action<LogEntry>? OnLogAdded = null;
	/// <summary>
	/// Event that is triggered whenever buffered logs have been written to file.
	/// </summary>
	public event Action? OnLogsWritten = null;

	#endregion
	#region Fields

	private readonly Queue<LogEntry> entries = new(64);
	private readonly int writeLogsEveryNEntries = 1;

	public readonly string applicationPath;
	public readonly string logDirAbsPath;
	public readonly string logFileAbsPath;

	private static readonly object lockObj = new();

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized { get; private set; } = false;

	public Engine Engine { get; }

	public static Logger? Instance { get; private set; } = null;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _disposing)
	{
		if (_disposing && IsInitialized)
		{
			Shutdown();
		}
		IsDisposed = true;
	}

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
			if (Instance is null || !Instance.IsInitialized)
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
		LogEntry entry = new(LogEntryType.Exception, _message, -1, LogEntrySeverity.Major, _exception);
		LogNewEntry(entry);
	}
	public void LogException(string _message, Exception _exception, LogEntrySeverity _severity = LogEntrySeverity.Normal)
	{
		LogEntry entry = new(LogEntryType.Exception, _message, -1, _severity, _exception);
		LogNewEntry(entry);
	}

	public void LogStatus(string _message, LogEntrySeverity _severity = LogEntrySeverity.Normal)
	{
		LogEntry entry = new(LogEntryType.Status, _message, 0, _severity);
		LogNewEntry(entry);
	}

	public unsafe void LogSdl2Error(int _errorCode, LogEntrySeverity _severity = LogEntrySeverity.Normal, bool _clearError = true)
	{
		if (_errorCode == 0) return;

		byte* pErrorMessageUtf8 = Sdl2Native.SDL_GetError();
		if (pErrorMessageUtf8 == null) return;

		uint length = PointerExt.GetUtf8Length((IntPtr)pErrorMessageUtf8, 1024u);
		string errorMessage = Encoding.UTF8.GetString(pErrorMessageUtf8, (int)length);

		LogError($"SDL2: {errorMessage}", _errorCode, _severity);

		if (_clearError)
		{
			Sdl2Native.SDL_ClearError();
		}
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

		// Notify any subscribers that a new log has been recorded:
		OnLogAdded?.Invoke(_entry);
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

		if (success)
		{
			OnLogsWritten?.Invoke();
		}
		return success;
	}

	#endregion
}

