namespace FragEngine3.EngineCore.Logging;

[Serializable]
public sealed class LogEntry(LogEntryType _type, string _message, long _errorCode = 0, LogEntrySeverity _severity = LogEntrySeverity.Normal, Exception? _exception = null) : IComparable<LogEntry>
{
	#region Fields

	public readonly LogEntryType type = _type;
	public readonly string message = _message ?? string.Empty;
	public readonly long errorCode = _errorCode;
	public readonly LogEntrySeverity severity = _severity;

	public readonly DateTime timestampUtc = DateTime.UtcNow;

	public readonly Type? exceptionType = _exception?.GetType();
	public readonly string exceptionMessage = _exception?.Message ?? string.Empty;
	public readonly string exceptionTrace = _exception?.StackTrace ?? string.Empty;

	public static readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;

	#endregion
	#region Constants

	private const string timeFormat = "yyyy.MM.dd HH:mm:ss";

	#endregion
	#region Methods

	public string FormatLogString()
	{
		string txt = type.IsThisGood()
			? $"{timestampUtc.ToString(timeFormat)} - {type}: {message}"
			: $"{timestampUtc.ToString(timeFormat)} - {type}: {message} [{errorCode} | {severity}]";

		if (type == LogEntryType.Exception)
		{
			txt += $"\n=> Exception type: {exceptionType?.ToString() ?? "Unknown"}";
			if (!string.IsNullOrEmpty(exceptionMessage))
			{
				txt += $"\n=> Exception message: {exceptionMessage}";
			}
			if (!string.IsNullOrEmpty(exceptionTrace))
			{
				txt += $"\n=> Exception trace: {exceptionTrace}";
			}
		}
		return txt;
	}

	public override string ToString()
	{
		string txt = type.IsThisGood()
			? message
			: $"{type}: {message} [{errorCode} | {severity}]";

		if (type == LogEntryType.Exception)
		{
			txt += $"\n=> Exception type: {exceptionType?.ToString() ?? "Unknown"}";
			if (!string.IsNullOrEmpty(exceptionMessage))
			{
				txt += $"\n=> Exception message: {exceptionMessage}";
			}
			if (!string.IsNullOrEmpty(exceptionTrace))
			{
				txt += $"\n=> Exception trace: {exceptionTrace}";
			}
		}
		return txt;
	}

	public ConsoleColor GetConsoleColor()
	{
		return type switch
		{
			LogEntryType.Message => defaultConsoleColor,
			LogEntryType.Warning => ConsoleColor.Yellow,
			LogEntryType.Error => ConsoleColor.Red,
			LogEntryType.Exception => ConsoleColor.DarkRed,
			LogEntryType.Status => ConsoleColor.Green,
			_ => defaultConsoleColor,
		};
	}

	public int CompareTo(LogEntry? other)
	{
		return other is not null ? timestampUtc.CompareTo(other.timestampUtc) : 1;
	}

	#endregion
}
