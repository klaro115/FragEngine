namespace FragEngine3.Containers;

/// <summary>
/// Status structure for tracking how far a complex and perhaps lengthy process has progressed so far.
/// This may be used to animate a loading bar during the initial loading stage on application start-up.
/// </summary>
public sealed class Progress
{
	#region Constructors

	public Progress(string _initialTaskTitle, int _taskCount)
	{
		taskTitle = _initialTaskTitle ?? string.Empty;
		tasksDone = 0;
		taskCount = Math.Max(_taskCount, 0);
		errorCount = 0;
	}

	#endregion
	#region Fields

	public string taskTitle = string.Empty;
	public int tasksDone;
	public int taskCount;
	public int errorCount;

	public readonly object lockObj = new();

	#endregion
	#region Properties

	/// <summary>
	/// Gets a percentage value of tasks done in the range [0;100].
	/// </summary>
	public float ProgressPercentage
	{
		get { lock (lockObj) { return (100.0f * tasksDone) / Math.Max(taskCount, 1); } }
	}

	/// <summary>
	/// Gets whether the process has concluded, no matter if in success or failure. Once this is raised through '<see cref="Finish"/>',
	/// no further calls to update or increment should be made. Other threads or systems may use this flag to check if the process'
	/// resources may be released.
	/// </summary>
	public bool IsFinished { get; private set; } = false;

	#endregion
	#region Methods

	/// <summary>
	/// Update the progress to represent a different part of the process.
	/// </summary>
	/// <param name="_taskTitle">A new title to represent the change in focus. If null, the current title is kept.</param>
	/// <param name="_tasksDone">The number of tasks that have already been completed.</param>
	/// <param name="_taskCount">The total number of tasks that must be completed. Must be greater than zero.</param>
	public void Update(string? _taskTitle, int _tasksDone, int _taskCount)
	{
		lock(lockObj)
		{
			if (_taskTitle != null) taskTitle = _taskTitle;
			tasksDone = Math.Max(_tasksDone, 0);
			taskCount = Math.Max(_taskCount, 1);
		}
	}

	/// <summary>
	/// Updates only the title to represent a different part of the process.
	/// </summary>
	/// <param name="_taskTitle">A new title to represent the change in focus. If null, the current title is kept.</param>
	public void UpdateTitle(string _taskTitle)
	{
		lock (lockObj)
		{
			if (_taskTitle != null) taskTitle = _taskTitle;
		}
	}

	public void Increment()
	{
		lock(lockObj)
		{
			tasksDone = Math.Clamp(tasksDone + 1, 0, taskCount);
		}
	}
	
	/// <summary>
	/// Finished the progress bar by setting the number of tasks done to equal the number of total tasks.
	/// </summary>
	public void CompleteAllTasks()
	{
		lock(lockObj)
		{
			tasksDone = taskCount;
		}
	}

	/// <summary>
	/// Mark the process as finished. No more calls to update or increment should be done afterwards.<para/>
	/// NOTE: This may be used to signal completion of an asynchronous process, and should therefore be called immediately once
	/// the process has succeeded, but also if it fails or aborted.
	/// </summary>
	public void Finish()
	{
		IsFinished = true;
	}

	public override string ToString()
	{
		return $"{taskTitle} {ProgressPercentage:0.0}% ({tasksDone} / {taskCount})";
	}

	#endregion
}
