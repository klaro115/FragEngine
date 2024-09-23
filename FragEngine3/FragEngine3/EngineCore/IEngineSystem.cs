namespace FragEngine3.EngineCore;

/// <summary>
/// Interface for identifying subsystems of an <see cref="EngineCore.Engine"/> instance.
/// </summary>
public interface IEngineSystem : IDisposable
{
	#region Properties

	/// <summary>
	/// Gets the engine instance that this system was created for.
	/// </summary>
	Engine Engine { get; }

	#endregion
}
