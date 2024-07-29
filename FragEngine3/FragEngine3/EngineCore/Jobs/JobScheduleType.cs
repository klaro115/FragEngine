using FragEngine3.Scenes;

namespace FragEngine3.EngineCore.Jobs;

/// <summary>
/// Enumeration of different schedules and threading options, used to control when a job is executed.<para/>
/// THREADING: Note that all jobs that are scheduled outside of the main thread must be thread-safe, aka they should not cause race conditions.<para/>
/// EXECUTION ORDER: If a job is queued up during the update stage it is scheduled to execute in, it is possible that the job will only start execution
/// during the next update cycle, even if the queue was empty. If you need something to execute now, using the job system is a bad idea; consider running
/// such tasks immediately on the executing thread instead.
/// </summary>
public enum JobScheduleType
{
	// GENERIC:

	/// <summary>
	/// Execute the job on the worker thread, start as soon as possible. Job must be thread-safe.
	/// </summary>
	WorkerThread			= 0,
	/// <summary>
	/// Start a new thread for the job, and start it immediately. Job must be thread-safe.
	/// </summary>
	NewThread,

	// UPDATE STAGES:

	/// <summary>
	/// Execute the job just before the start of the engine's <see cref="SceneUpdateStage.Early"/> stage, on the main thread.
	/// </summary>
	MainThread_PreUpdate	= 10,
	/// <summary>
	/// Execute the job right after the end of the engine's <see cref="SceneUpdateStage.Late"/> stage, on the main thread.
	/// </summary>
	MainThread_PostUpdate,
	/// <summary>
	/// Execute the job at the start of the engine's <see cref="SceneUpdateStage.Fixed"/> stage, on the main thread. Intended for jobs that directly effect the physics engine.
	/// </summary>
	MainThread_FixedUpdate,

	// DRAW STAGES:

	/// <summary>
	/// Execute the job before draw calls are generated and processed by the GPU, on the main thread. GPU might still be rendering previous frame at time of execution.
	/// </summary>
	MainThread_PreDraw		= 20,
	/// <summary>
	/// Execute the job after draw calls have been generated and sent to the GPU, on the main thread. GPU might still be rendering current frame at time of execution.
	/// </summary>
	MainThread_PostDraw,
}

public static class JobScheduleTypeExt
{
	#region Methods

	/// <summary>
	/// Gets whether this schedule type will execute on the main thread.
	/// </summary>
	/// <param name="_scheduleType">This schedule type.</param>
	/// <returns>True if executing on main thread, false otherwise.</returns>
	public static bool IsScheduledOnMainThread(this JobScheduleType _scheduleType)
	{
		return _scheduleType >= JobScheduleType.MainThread_PreUpdate;
	}

	#endregion
}
