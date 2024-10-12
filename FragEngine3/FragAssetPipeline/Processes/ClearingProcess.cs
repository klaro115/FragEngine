namespace FragAssetPipeline.Processes;

internal static class ClearingProcess
{
	#region Methods

	public static bool ClearFolder(string _directoryPath, bool _recursive)
	{
		if (string.IsNullOrEmpty(_directoryPath))
		{
			Program.PrintError("Cannot clear folder at null or blank path!");
			return false;
		}
		if (!Directory.Exists(_directoryPath))
		{
			Program.PrintError("Cannot clear folder at null or blank path!");
			return false;
		}

		try
		{
			IEnumerable<string> fileEnumerator = Directory.EnumerateFiles(_directoryPath);
			foreach (string filePath in fileEnumerator)
			{
				File.Delete(filePath);
			}
		}
		catch (Exception ex)
		{
			Program.PrintError($"An exception was caught while trying to a clear folder!\nFolder path: '{_directoryPath}'\nException message: '{ex.Message}'");
			return false;
		}

		if (_recursive)
		{
			try
			{
				IEnumerable<string> dirEnumerator = Directory.EnumerateDirectories(_directoryPath);
				foreach (string dirPath in dirEnumerator)
				{
					Directory.Delete(dirPath, true);
				}
			}
			catch (Exception ex)
			{
				Program.PrintError($"An exception was caught while trying to a clear sub-directories!\nFolder path: '{_directoryPath}'\nException: '{ex.Message}'");
				return false;
			}
		}

		return true;
	}

	#endregion
}
