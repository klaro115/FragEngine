using System.Text;

namespace FragEngine3.Utility;

/// <summary>
/// Helper class with method for processing file or directory paths.
/// </summary>
public static class PathTools
{
	#region Types

	/// <summary>
	/// Structure mapping a specific sub-section of a file or directory path.
	/// </summary>
	/// <param name="_path">The underlying path that the part lies on.</param>
	/// <param name="_spanStartIndex">The index of the part's first character.</param>
	/// <param name="_spanLength">The number of UTF-16 characters that make up the part.</param>
	public readonly struct PathPart(string _path, int _spanStartIndex, int _spanLength)
	{
		public readonly string path = _path;
		public readonly int startIndex = _spanStartIndex;
		public readonly int length = _spanLength;

		/// <summary>
		/// Gets a read-only span of characters of the base path that this part maps to.
		/// </summary>
		public ReadOnlySpan<char> Span => path.AsSpan(startIndex, length);
	}

	#endregion
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

	/// <summary>
	/// Gets a list of all parts of the given path.<pata/>
	/// Note: This uses <see cref="IteratePathParts(string)"/> internally and stores all results in a list.
	/// </summary>
	/// <param name="_path">A file or directory path that you wish to take apart.</param>
	/// <returns>A list of parts.</returns>
	public static List<PathPart> GetPathParts(string _path)
	{
		List<PathPart> parts = [];
		IEnumerator<PathPart> e = IteratePathParts(_path);
		while (e.MoveNext())
		{
			parts.Add(e.Current);
		}
		return parts;
	}

	/// <summary>
	/// Gets an enumerator for iterating over all parts of a path.
	/// </summary>
	/// <param name="_path">The path whose parts we wish to pick apart.</param>
	public static IEnumerator<PathPart> IteratePathParts(string _path)
	{
		if (string.IsNullOrEmpty(_path)) yield break;

		int partStartIdx = 0;
		for (int i = 0; i < _path.Length; ++i)
		{
			char c = _path[i];
			if (c == '/' || c == '\\')
			{
				int partLength = i - partStartIdx;
				if (partLength > 0)
				{
					yield return new(_path, partStartIdx, partLength);
					partStartIdx = i + 1;
				}
			}
		}
		if (partStartIdx < _path.Length)
		{
			int partLength = _path.Length - partStartIdx;
			yield return new(_path, partStartIdx, partLength);
		}
	}

	/// <summary>
	/// Try to find the common root directory from across multiple file paths.
	/// </summary>
	/// <param name="_paths">A enumeration of file or directory paths. It is expected that all paths are either
	/// absolute or relative to the same root directory. This method checks just how far up the shared directory
	/// path goes.</param>
	/// <returns>A common path that all other paths are relative to.</returns>
	public static string GetCommonBaseDirectoryPath(IEnumerable<string> _paths)
	{
		if (_paths is null || !_paths.Any())
		{
			return string.Empty;
		}
		if (_paths.Count() == 1)
		{
			return Path.GetDirectoryName(_paths.First()) ?? string.Empty;
		}

		// Take the first path's directory, and split it into parts for comparison:
		string? firstDirPath = Path.GetDirectoryName(_paths.First());
		if (string.IsNullOrEmpty(firstDirPath))
		{
			return string.Empty;
		}
		List<PathPart> parts = GetPathParts(firstDirPath);
		int maxOverlapCount = parts.Count;

		foreach (string path in _paths)
		{
			// Check if parts match, drop later parts immediately upon first mismatch:
			int curOverlapCount = 0;
			IEnumerator<PathPart> e = IteratePathParts(path);
			while (
				e.MoveNext() &&
				curOverlapCount < maxOverlapCount &&
				e.Current.Span.Equals(parts[curOverlapCount].Span, StringComparison.OrdinalIgnoreCase))
			{
				curOverlapCount++;
			}

			maxOverlapCount = curOverlapCount;
			if (maxOverlapCount == 0)
			{
				break;
			}
		}

		// Assemble all overlapping parts back into one path string:
		StringBuilder builder = new(firstDirPath.Length);
		for (int i = 0; i < maxOverlapCount; ++i)
		{
			if (i != 0)
			{
				builder.Append(Path.PathSeparator);
			}
			builder.Append(parts[i]);
		}
		return builder.ToString();
	}

	#endregion
}
