namespace FragEngine3.EngineCore.Logging;

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
	/// <summary>
	/// Gets whether this log entry type is a positive or a negative thing.
	/// Good/neutral things are messages and status updates, bad things are warnings, errors, and exceptions.
	/// </summary>
	/// <param name="_type"></param>
	/// <returns>True if the message is either positive or neutral, or false, if its considered negative.</returns>
	public static bool IsThisGood(this LogEntryType _type)
	{
		return (int)_type < LoggerConstants.LOG_ENTRY_TYPE_BAD_INDEX;
	}
}
