namespace FragEngine3.EngineCore.Health;

internal sealed class HealthCheckQueue(Engine _engine, Action<HealthCheck> _checkFoundAbortConditionEvent, Action<HealthCheck> _checkFoundWarningConditionEvent)
{
	#region Fields

	private readonly Engine engine = _engine;
	private readonly Action<HealthCheck> checkFoundAbortConditionEvent = _checkFoundAbortConditionEvent;
	private readonly Action<HealthCheck> checkFoundWarningConditionEvent = _checkFoundWarningConditionEvent;

	private readonly List<HealthCheck> queue = [];

	private readonly object lockObj = new();

	#endregion
	#region Properties

	public int Count => queue.Count;

	#endregion
	#region Methods

	public bool AddCheck(HealthCheck _newCheck)
	{
		lock (lockObj)
		{
			if (queue.Contains(_newCheck))
			{
				return false;
			}

			ScheduleCheck(_newCheck, _newCheck.FirstCheckDelay);
		}
		return true;
	}

	public bool HasCheck(int _checkId)
	{
		lock (lockObj)
		{
			return queue.Any(o => o.id == _checkId);
		}
	}

	public bool RemoveCheck(HealthCheck _check)
	{
		if (_check is null)
		{
			return true;
		}

		lock (lockObj)
		{
			return queue.Remove(_check);
		}
	}

	public bool RemoveCheck(int _checkId)
	{
		lock (lockObj)
		{
			return queue.RemoveAll(o => o.id == _checkId) != 0;
		}
	}

	public void RemoveAllChecks()
	{
		lock (lockObj)
		{
			queue.Clear();
		}
	}

	/// <summary>
	/// Queues up a new check at a given time.
	/// </summary>
	/// <param name="_check">The new check that shall be inserted into the queue.</param>
	/// <param name="_timeFromNow">A timestamp for when the check should first be executed, relative to now.</param>
	private void ScheduleCheck(HealthCheck _check, TimeSpan _timeFromNow)
	{
		_check.nextCheckTime = DateTime.UtcNow + _timeFromNow;  //TODO: Use timestamp from TimeManager instead!

		if (queue.Count == 0)
		{
			queue.Add(_check);
			return;
		}
		if (_check.nextCheckTime <= queue[0].nextCheckTime)
		{
			queue.Insert(0, _check);
			return;
		}

		int lower = 0;
		int upper = queue.Count;
		int mid = upper;
		while (lower < upper)
		{
			mid = lower + upper >> 1;
			DateTime midTime = queue[mid].nextCheckTime;

			if (midTime == _check.nextCheckTime)
			{
				break;
			}
			else if (midTime > _check.nextCheckTime)
			{
				upper = mid;
			}
			else
			{
				lower = mid;
			}
		}

		queue.Insert(mid, _check);
	}

	/// <summary>
	/// Peek at the first element in the queue, which is next in line for execution. This will not dequeue the check.
	/// </summary>
	/// <param name="_outCheck">Outputs the first check in the queue, or null, if the queue is empty.</param>
	/// <returns>True if at least one check was queued up, false if the queue is empty.</returns>
	public bool PeekCheck(out HealthCheck? _outCheck)
	{
		lock (lockObj)
		{
			if (queue.Count == 0)
			{
				_outCheck = null;
				return false;
			}

			_outCheck = queue[0];
		}
		return true;
	}

	/// <summary>
	/// Dequeues a check and executes it. If the check is on a repeating schedule, it will be reinserted into the queue.
	/// </summary>
	/// <param name="_currentCheck">The next check in line, retrieved via <see cref="PeekCheck"/>.</param>
	/// <returns>True if the check was performed, or false, if the check resulted in an abort condition.</returns>
	public bool PopAndExecuteCheck(HealthCheck _currentCheck)
	{
		lock (lockObj)
		{
			queue.Remove(_currentCheck);
			if (_currentCheck.RepeatCheck)
			{
				ScheduleCheck(_currentCheck, _currentCheck.RepetitionInterval);
			}
		}

		HealthCheckRating rating = _currentCheck.performCheckCallback(engine);
		if (rating >= _currentCheck.AbortThreshold)
		{
			checkFoundAbortConditionEvent?.Invoke(_currentCheck);
			return false;
		}
		else if (rating >= _currentCheck.WarningThreshold)
		{
			checkFoundWarningConditionEvent?.Invoke(_currentCheck);
		}

		_currentCheck.repetitionCount++;
		return true;
	}

	#endregion
}
