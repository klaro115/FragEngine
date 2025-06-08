namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for the <see cref="List{T}"/> class.
/// </summary>
public static class ListExt
{
	#region Methods

	/// <summary>
	/// Selectively adds elements from a source enumeration that meet a criterion.
	/// </summary>
	/// <typeparam name="T">The type of items in the list.</typeparam>
	/// <param name="_dstList">This list, where we want to add items.</param>
	/// <param name="_itemsSource">Another enumeration from which items may be added to the list.</param>
	/// <param name="_funcSelector">Function delegate that checks if an item from the source enumeration should be added to the list.</param>
	/// <returns>The resulting list.</returns>
	/// <exception cref="ArgumentNullException">Destination list, items source, and selector function may not be null!</exception>
	public static List<T> AddWhere<T>(this List<T> _dstList, IEnumerable<T> _itemsSource, Func<T, bool> _funcSelector)
	{
		if (_dstList is null)
			throw new ArgumentNullException(nameof(_dstList), "Destination list may not be null!");
		if (_itemsSource is null)
			throw new ArgumentNullException(nameof(_itemsSource), "Items source enumeration may not be null!");
		if (_funcSelector is null)
			throw new ArgumentNullException(nameof(_funcSelector), "Selector function may not be null!");

		if (_dstList == _itemsSource)
		{
			return _dstList;
		}

		foreach (T item in _itemsSource)
		{
			if (_funcSelector(item))
			{
				_dstList.Add(item);
			}
		}

		return _dstList;
	}

	/// <summary>
	/// Disposes all elements in the list, then clears the list.
	/// </summary>
	/// <typeparam name="T">The type of items in the list, must implement the <see cref="IDisposable"/> interface.</typeparam>
	/// <param name="_list">This list, whose contents we want to clear out safely.</param>
	/// <exception cref="ArgumentNullException">List may not be null!</exception>
	public static void DisposeAndClear<T>(this List<T> _list) where T : IDisposable
	{
		if (_list is null)
			throw new ArgumentNullException(nameof(_list), "List may not be null!");

		foreach (T item in _list)
		{
			item?.Dispose();
		}
		_list.Clear();
	}

	#endregion
}
