namespace FragEngine3.Utility;

public static class PathTools
{
	#region Fields

	private static readonly char[] separators = { '/', '\\' };

	#endregion
	#region Methods

	public static string GetLastPartName(string _path)
	{
		if (string.IsNullOrEmpty(_path)) return string.Empty;

		int nameEnd = _path.Length;
		for (int i = _path.Length - 1; i >= 0; i--)
		{
			if (_path[i] != '/' && _path[i] != '\\')
			{
				nameEnd = i + 1;
				break;
			}
		}
		if (nameEnd <= 0) return string.Empty;

		int lastPartStartIdx = _path.LastIndexOfAny(separators, nameEnd - 1) + 1;
		return lastPartStartIdx >= 0
			? _path[lastPartStartIdx..nameEnd]
			: string.Empty;
	}

	#endregion
}
