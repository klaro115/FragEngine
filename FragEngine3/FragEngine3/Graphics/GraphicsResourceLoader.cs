using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Import;

namespace FragEngine3.Graphics;

/// <summary>
/// Container service for all graphics resource loaders and importers managed by the engine.
/// </summary>
public sealed class GraphicsResourceLoader : IDisposable
{
	#region Constructors

	public GraphicsResourceLoader(Engine _engine)
	{
		engine = _engine;

		modelImporter = new(engine.ResourceManager, engine.GraphicsSystem.graphicsCore);
		shaderImporter = new(engine.ResourceManager, engine.GraphicsSystem.graphicsCore);
		//...
	}

	~GraphicsResourceLoader()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly Engine engine;

	public readonly ModelImporter modelImporter;
	public readonly ShaderImporter shaderImporter;
	//...

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	private void Dispose(bool _)
	{
		IsDisposed = true;

		modelImporter.Dispose();
		shaderImporter.Dispose();
		//...
	}

	#endregion
}
