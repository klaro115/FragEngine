﻿using FragEngine3.Graphics.Config;

namespace FragEngine3.EngineCore.Config;

[Serializable]
public sealed class EngineConfig
{
	#region Properties

	public string ApplicationName { get; set; } = "EngineTest";
	public string MainWindowTitle { get; set; } = "EngineTest";
	
	public GraphicsConfig Graphics { get; set; } = new();
	//...

	#endregion
	#region Methods

	/// <summary>
	/// Creates a deep copy of this engine config instance.
	/// </summary>
	public EngineConfig Clone()
	{
		return new EngineConfig()
		{
			Graphics = Graphics.Clone(),
			//...
		};
	}

	#endregion
}
