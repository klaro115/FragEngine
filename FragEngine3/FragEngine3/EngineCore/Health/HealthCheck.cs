namespace FragEngine3.EngineCore.Health;

/// <summary>
/// A health check that can be scheduled to execute repeatedly at fixed intervals.
/// </summary>
/// <param name="_id">A unique ID number for indentifying this check later on. It is recommended to use fixed
/// constant values for IDs of recurring checks, so that you may cancel or reschedule them on demand.</param>
/// <param name="_performCheckCallback">A callback through which the <see cref="HealthCheckSystem"/> is notified
/// that a check has detected an unstable state.</param>
/// <param name="_executeOnMainThread">A callback through which the <see cref="HealthCheckSystem"/> is notified
/// that a check has detected an abort condition, whereupon the engine should immediately attempt to exit safely.</param>
public sealed class HealthCheck(int _id, Func<Engine, HealthCheckRating> _performCheckCallback, bool _executeOnMainThread = true)
{
	#region Fields

	private string? name = null;

	public readonly int id = _id;
	public readonly Func<Engine, HealthCheckRating> performCheckCallback = _performCheckCallback;
	public readonly bool executeOnMainThread = _executeOnMainThread;

	public uint repetitionCount = 0;
	internal DateTime nextCheckTime = DateTime.UtcNow;

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets the name for this check. If null or empty, a generic name based on the check's ID is returned.
	/// </summary>
	public string Name
	{
		get => string.IsNullOrEmpty(name) ? $"Check{id}" : name;
		set => name = string.IsNullOrEmpty(value) ? null : value;
	}

	/// <summary>
	/// Whether this check should be executed repeatedly at fixed time intervals. If false, it is only executed once.
	/// </summary>
	public bool RepeatCheck { get; init; } = true;
	/// <summary>
	/// A time delay between the moment the check is first scheduled, and the time it should be executed for the first time.
	/// </summary>
	public TimeSpan FirstCheckDelay { get; init; } = TimeSpan.Zero;
	/// <summary>
	/// A time interval between repeated executions of the check. This does nothing if <see cref="RepeatCheck"/> is false.
	/// </summary>
	public TimeSpan RepetitionInterval { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// The outcome of the check at which a system has become worrysome. If the <see cref="performCheckCallback"/> returns
	/// a rating that matches or exceeds this rating, a warning is logged.
	/// </summary>
	public HealthCheckRating WarningThreshold { get; init; } = HealthCheckRating.MinorIssues;
	/// <summary>
	/// The outcome of the check at which a system has become compromised. If the <see cref="performCheckCallback"/> returns
	/// a rating that matches or exceeds this rating, it is considered as an abort condition. The engine will attempt to exit
	/// immediately if this happens.
	/// </summary>
	public HealthCheckRating AbortThreshold { get; init; } = HealthCheckRating.Compromised;

	#endregion
	#region Methods

	/// <summary>
	/// Checks whether this check's parameters make sense.
	/// </summary>
	/// <returns>True if the check is valid, false otherwise.</returns>
	public bool IsValid()
	{
		bool result =
			FirstCheckDelay >= TimeSpan.Zero &&
			(!RepeatCheck || RepetitionInterval > TimeSpan.Zero) &&
			WarningThreshold > HealthCheckRating.Nominal &&
			AbortThreshold >= WarningThreshold &&
			performCheckCallback is not null;
		return result;
	}

	public override string ToString()
	{
		string repetitionTxt = RepeatCheck ? $"Repeating (Interval={RepetitionInterval})" : "Once";
		return $"{Name} (ID={id}), {repetitionTxt}, Counter={repetitionCount}, Valid={IsValid()}";
	}

	#endregion
}
