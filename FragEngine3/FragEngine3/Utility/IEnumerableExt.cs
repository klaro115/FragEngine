namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for the <see cref="IEnumerable{T}"/> interface.
/// </summary>
public static class IEnumerableExt
{
	#region Methods

	/// <summary>
	/// Dispose all elements within this enumerable.
	/// </summary>
	/// <typeparam name="TElement">The type of elements in the enumeration, must implement the <see cref="IDisposable"/> interface.</typeparam>
	/// <param name="_enumerable">Thsi enumerable, whose elements we want to dispose.</param>
	/// <exception cref="ArgumentNullException">The enumerable may not be null.</exception>
	public static void DisposeElements<TElement>(this IEnumerable<TElement> _enumerable)
		where TElement : IDisposable
	{
		if (_enumerable is null)
			throw new ArgumentNullException(nameof(_enumerable), "Enumerable may not be null!");

		foreach (TElement element in _enumerable)
		{
			element?.Dispose();
		}
	}

	#endregion
}
