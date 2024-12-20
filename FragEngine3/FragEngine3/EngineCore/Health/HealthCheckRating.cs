namespace FragEngine3.EngineCore.Health;

/// <summary>
/// Enumeration of possible rating results when performing a <see cref="HealthCheck"/>.
/// </summary>
public enum HealthCheckRating
{
	/// <summary>
	/// Undefined or unconclusive result, or the health status could not be checked at this time.
	/// </summary>
	Unclear,

	/// <summary>
	/// The checked system operates as intended, with no issues worth mentioning.
	/// </summary>
	Nominal,
	/// <summary>
	/// The checked system has encountered some issues, but can continue to operate without compromising other systems.
	/// </summary>
	MinorIssues,
	/// <summary>
	/// The checked system has encountered significant issues, and is at risk of being compromised or compromising other systems.
	/// It would be wise to shut down and reboot the concerned system ASAP.
	/// </summary>
	MajorIssues,
	/// <summary>
	/// The checked system is no longer in an operational state, and keeping it running any further will result in crashes or data loss.
	/// This is typically a result that would warrant the engine to shut down ASAP. In some lenient cases, it might suffice to fully
	/// terminate and reboot the compromised system.
	/// </summary>
	Compromised,
}
