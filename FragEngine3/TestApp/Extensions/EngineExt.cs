using FragEngine3.EngineCore;
using TestApp.Graphics;

namespace TestApp.Extensions;

/// <summary>
/// Extension methode for the <see cref="Engine"/> class.
/// </summary>
public static class EngineExt
{
	#region Fields

	private static readonly Type[] constantBufferDataTypes =
	[
		typeof(CBShadowMapVisualizer),
		//...
	];	

	#endregion
	#region Methods

	/// <summary>
	/// Registers additional non-engine types with the engine's systems.
	/// </summary>
	/// <param name="_engine">This engine instance.</param>
	/// <returns></returns>
	public static bool RegisterTestAppTypes(this Engine _engine)
	{
		bool success = true;
		foreach (Type dataType in constantBufferDataTypes)
		{
			success &= _engine.GraphicsTypeRegistry.RegisterConstantBufferType(dataType);
		}
		return success;
	}

	#endregion
}
