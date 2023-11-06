using FragEngine3.EngineCore.Logging;

namespace FragEngine3.EngineCore
{
	public sealed class Logger
	{
		#region Constructors

		public Logger(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		}

		#endregion
		#region Fields

		public readonly Engine engine;

		private readonly List<LogEntry> entries = new(256);
		private readonly int maxEntryCount = 256;
		private readonly int writeLogsEveryNEntries = 1;

		#endregion
		#region Methods

		public void LogMessage(string _message)
		{
			LogEntry entry = new(LogEntryType.Message, _message);
			LogNewEntry(entry);
		}

		public void LogNewEntry(LogEntry _entry)
		{
			entries.Add(_entry);

			if (entries.Count > maxEntryCount)
			{
				int excessCount = entries.Count - maxEntryCount;
				for (int i = 0; i < excessCount; ++i)
				{
					WriteLogs(entries, 0, excessCount);
				}
				entries.RemoveRange(0, excessCount);
			}
		}

		public bool WriteLogs()
		{
			//TODO
			return false;
		}

		public bool WriteLogs(IList<LogEntry> _list, int _startIdx, int _count)
		{
			//TODO
			return false;
		}

		#endregion
	}
}

