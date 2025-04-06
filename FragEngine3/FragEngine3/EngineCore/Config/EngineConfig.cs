using FragEngine3.EngineCore.Logging;
using FragEngine3.Graphics.Config;
using FragEngine3.Utility;

namespace FragEngine3.EngineCore.Config;

[Serializable]
public sealed class EngineConfig
{
	#region Properties

	public required string ApplicationName { get; init; } = "EngineTest";
	public required string MainWindowTitle { get; init; } = "EngineTest";

	public required LoggerConfig Logger { get; init; } = new();
	public required GraphicsConfig Graphics { get; init; } = new();
	//...

	#endregion
	#region Methods

	/// <summary>
	/// Creates a deep copy of this engine config instance.
	/// </summary>
	public EngineConfig Clone()
	{
		string clonedApplicationName = ApplicationName.AddIncrementalIndexSuffix(1);
		string clonedMainWindowName = MainWindowTitle.AddIncrementalIndexSuffix(1);

		return new EngineConfig()
		{
			ApplicationName = clonedApplicationName,
			MainWindowTitle = clonedMainWindowName,

			Logger = Logger.Clone(),
			Graphics = Graphics.Clone(),
			//...
		};
	}

	#endregion
}
