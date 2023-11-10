
namespace FragEngine3.EngineCore.Logging
{
	[Serializable]
	public sealed class LogEntry : IComparable<LogEntry>
	{
		#region Constructors

		public LogEntry(LogEntryType _type, string _message, long _errorCode = 0, LogEntrySeverity _severity = LogEntrySeverity.Normal, Exception? _exception = null)
		{
			type = _type;
			message = _message ?? string.Empty;
			errorCode = _errorCode;
			severity = _severity;

			timestampUtc = DateTime.UtcNow;

			exceptionType = _exception?.GetType();
			exceptionMessage = _exception?.Message ?? string.Empty;
			exceptionTrace = _exception?.StackTrace ?? string.Empty;
		}

		#endregion
		#region Fields

		public readonly LogEntryType type;
		public readonly string message;
		public readonly long errorCode;
		public readonly LogEntrySeverity severity;

		public readonly DateTime timestampUtc;

		public readonly Type? exceptionType = null;
		public readonly string exceptionMessage = string.Empty;
		public readonly string exceptionTrace = string.Empty;

		public static readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;

		#endregion
		#region Constants

		private const string timeFormat = "yyyy.MM.dd HH:mm:ss.FFF";

		#endregion
		#region Methods

		public override string ToString()
		{
			string txt = $"{timestampUtc.ToString(timeFormat)} - {type}: {message} [{errorCode} | {severity}]";
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
			return other != null ? timestampUtc.CompareTo(other.timestampUtc) : 1;
		}

		#endregion
	}
}
