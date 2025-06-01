using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Internal;

/// <summary>
/// A re-usable pool of command lists.<para/>
/// OWNERSHIP: This pool object wil, have ownership of any command lists that are managed or created by it.
/// Do not dispose command lists that were issued by this pool; dispose the entire pool instead.
/// </summary>
/// <param name="_graphicsCore">A graphics core for which command lists will be created.</param>
/// <param name="_initialCapacity">The initial capacity for command list that shall be allocated from the start.</param>
internal sealed class CommandListPool(GraphicsCore _graphicsCore, uint _initialCapacity = 4) : IDisposable
{
	#region Constructors

	~CommandListPool()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(graphicsCore), "Graohics core may not be null!");
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

	private readonly Stack<CommandList> pool = new((int)_initialCapacity);
	private readonly List<CommandList> inUse = new((int)_initialCapacity);

	#endregion
	#region Properties

	/// <summary>
	/// Gets whether the pool has been disposed.
	/// </summary>
	public bool IsDisposed { get; private set; } = false;

	/// <summary>
	/// Gets the total number of command lists that are currently owned and managed by this pool.
	/// </summary>
	public int TotalPoolSize => pool.Count + inUse.Count;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
	{
		IsDisposed = true;
		Clear();
	}

	/// <summary>
	/// Removes and disposes all command lists managed by this pool.
	/// </summary>
	public void Clear()
	{
		foreach (CommandList cmdList in inUse)
		{
			cmdList.Dispose();
		}
		while (pool.TryPop(out CommandList? cmdList))
		{
			cmdList.Dispose();
		}
		inUse.Clear();
		pool.Clear();
	}

	/// <summary>
	/// Returns all command lists that have been in use to the pool of available command lists.
	/// </summary>
	public bool ReturnUsedToPool()
	{
		if (IsDisposed)
		{
			logger.LogError("Command list pool has already been disposed!");
			return false;
		}

		foreach (CommandList cmdList in inUse)
		{
			if (!cmdList.IsDisposed)
			{
				pool.Push(cmdList);
			}
		}
		inUse.Clear();
		return true;
	}

	/// <summary>
	/// Tries to get or create a command list.
	/// Any unused command lists from the pool are re-used first; if none remain in the pool, a new command list is created instead.
	/// </summary>
	/// <param name="_outCmdList">Outputs a command list, or null, if creating a new list fails.</param>
	/// <returns>True if a command list could be provided, false on error.</returns>
	public bool GetOrCreateCommandList(out CommandList? _outCmdList)
	{
		if (IsDisposed)
		{
			logger.LogError("Command list pool has already been disposed!");
			_outCmdList = null;
			return false;
		}

		// Try re-using an existing command list from pool:
		bool success = pool.TryPop(out _outCmdList) && !_outCmdList.IsDisposed;
		if (!success)
		{
			// If none available, create a new one:
			success = graphicsCore.CreateCommandList(out _outCmdList);
		}

		// Mark the command list as being in use:
		if (success)
		{
			inUse.Add(_outCmdList!);
		}
		return success;
	}

	/// <summary>
	/// Manually returns a single command list (that is currently in use) to the pool.
	/// </summary>
	/// <param name="_cmdList">A command list that is part of this pool, and was in use until just now.</param>
	/// <returns>True if the command list was returned to the pool, false otherwise.<para/>
	/// Note: Consider disposing the command list if this returns false, as the object is likely not in a location or lifecycle state where it belongs.</returns>
	public bool ReturnCommandListToPool(CommandList? _cmdList)
	{
		if (IsDisposed)
		{
			logger.LogError("Command list pool has already been disposed!");
			return false;
		}

		// Check if the list is a valid member of this pool, and currently in use:
		if (_cmdList is null || _cmdList.IsDisposed)
		{
			logger.LogError("Cannot return null or disposed command list to pool!");
			return false;
		}
		if (!inUse.Contains(_cmdList))
		{
			logger.LogError("Cannot return command list to pool; it couldn't be found in the 'in-use' list!");
			return false;
		}

		// Remove from in-use list, and return to pool:
		bool success = inUse.Remove(_cmdList);
		if (success)
		{
			pool.Push(_cmdList);
		}
		return success;
	}
	
	#endregion
}
