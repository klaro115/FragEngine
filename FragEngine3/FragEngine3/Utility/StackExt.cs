namespace FragEngine3.Utility;

/// <summary>
/// Extension methods for the <see cref="Stack{T}"/> class.
/// </summary>
public static class StackExt
{
	#region Methods

	/// <summary>
	/// Disposes all elements on the stack and clears it.
	/// </summary>
	/// <typeparam name="T">The type of items on the stack, must implement the <see cref="IDisposable"/> interface.</typeparam>
	/// <param name="_list">This stack, whose contents we want to clear out safely.</param>
	/// <exception cref="ArgumentNullException">Stack may not be null!</exception>
	public static void DisposeAndClear<T>(this Stack<T> _stack) where T : IDisposable
	{
		if (_stack is null)
			throw new ArgumentNullException(nameof(_stack), "Stack may not be null!");

		while (_stack.TryPop(out T? item))
		{
			item?.Dispose();
		}
		_stack.Clear();
	}

	#endregion
}
