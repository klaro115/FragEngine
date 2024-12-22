namespace FragAssetFormats.Contexts;

public interface ILogger
{
	#region Methods

	void LogWarning(string _message);
	void LogError(string _message);
	void LogException(string _message, Exception _exception);

	#endregion
}
