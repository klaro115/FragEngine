namespace FragEngine3.EngineCore.Jobs;

/// <summary>
/// Callback function that executes a job's workload.
/// </summary>
/// <returns>True if the job finished successfully, false otherwise.</returns>
public delegate bool FuncJobAction();

/// <summary>
/// Callback function for when a job has ended.
/// </summary>
/// <param name="_wasCompleted">Whether the job has run through completely. If false, it was aborted or met with an error.</param>
/// <param name="_actionReturnValue">Whether the job's workload was completed successfully. Thsi is the return value of the job's action delegate.</param>
public delegate void FuncJobEndedCallback(bool _wasCompleted, bool _actionReturnValue);

/// <summary>
/// Callback function for notifying <see cref="JobManager"/> that a job has ended.
/// </summary>
/// <param name="_job">The job that has ended or was aborted.</param>
/// <param name="_executeEndedCallback">Whether to execute the job's <see cref="FuncJobEndedCallback"/> function.</param>
internal delegate void FuncJobStatusChanged(Job _job, bool _executeEndedCallback);
