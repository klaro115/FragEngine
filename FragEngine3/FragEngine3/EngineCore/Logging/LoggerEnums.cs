
namespace FragEngine3.EngineCore.Logging
{
	public enum LogEntryType
	{
		// Good stuff:
		Message		= 0,
		Status,

		// Bad stuff:
		Warning		= LoggerConstants.LOG_ENTRY_TYPE_BAD_INDEX,
		Error,
		Exception,
	}

	public enum LogEntrySeverity
	{
		Trivial,
		Normal,
		Major,
		Critical,
	}

	public static class LogEntryEnumExt
	{
		public static bool IsThisGood(this LogEntryType _type)
		{
			return (int)_type < LoggerConstants.LOG_ENTRY_TYPE_BAD_INDEX;
		}
	}
}
