using FragEngine3.EngineCore.Logging;

namespace FragAssetPipeline.Common;

internal sealed class ConsoleLogger : ILogger
{
	#region Fields

	private readonly object lockObj = new();

	#endregion
	#region Properties

	public bool IsInitialized => true;

	#endregion
	#region Methods

	public void LogMessage(string _message, bool _dontPrintToConsole = false)
	{
		if (!_dontPrintToConsole)
		{
			lock(lockObj)
			{
				Console.WriteLine(_message);
			}
		}
	}

	public void LogWarning(string _message)
	{
		lock(lockObj)
		{
			ConsoleColor prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine(_message);
			Console.ForegroundColor = prevColor;
		}
	}

	public void LogError(string _message)
	{
		lock(lockObj)
		{
			ConsoleColor prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(_message);
			Console.ForegroundColor = prevColor;
		}
	}

	public void LogException(string _message, Exception _exception)
	{
		lock(lockObj)
		{
			ConsoleColor prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkRed;
			Console.WriteLine(_message);
			Console.WriteLine($"Exception: {_exception}");
			Console.ForegroundColor = prevColor;
		}
	}

	#endregion
}
