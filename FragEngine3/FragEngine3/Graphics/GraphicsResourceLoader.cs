using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Import;

namespace FragEngine3.Graphics;

/// <summary>
/// Container service for all graphics resource loaders and importers managed by the engine.
/// </summary>
public sealed class GraphicsResourceLoader : IDisposable
{
	#region Constructors

	/// <summary>
	/// Creates a new container service for managing import of graphics resources.
	/// </summary>
	/// <param name="_engine">The engine that this service is created for.</param>
	public GraphicsResourceLoader(Engine _engine)
	{
		engine = _engine;

		modelImporter = new(engine.ResourceManager, engine.GraphicsSystem.graphicsCore);
		shaderImporter = new(engine.ResourceManager, engine.GraphicsSystem.graphicsCore);
		imageImporter = new(engine.ResourceManager, engine.GraphicsSystem.graphicsCore);
		//...
	}

	~GraphicsResourceLoader()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly Engine engine;

	/// <summary>
	/// The service responsible for managing 3D model import.
	/// </summary>
	internal readonly ModelImporter modelImporter;
	/// <summary>
	/// The service responsible for managing shader import.
	/// </summary>
	internal readonly ShaderImporter shaderImporter;
	/// <summary>
	/// The service responsible for managing image and texture import.
	/// </summary>
	internal readonly ImageImporter imageImporter;
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

	/// <summary>
	/// Tries to register a new shader importer.
	/// </summary>
	/// <param name="_newImporter">The new importer instance.</param>
	/// <returns>True if the importer was registered successfully, false otherwise.</returns>
	public bool RegisterShaderImporter(IShaderImporter _newImporter)
	{
		return !IsDisposed && shaderImporter.RegisterImporter(_newImporter);
	}

	/// <summary>
	/// Tries to register a new 3D model importer.
	/// </summary>
	/// <param name="_newImporter">The new importer instance.</param>
	/// <returns>True if the importer was registered successfully, false otherwise.</returns>
	public bool RegisterModelImporter(IModelImporter _newImporter)
	{
		return !IsDisposed && modelImporter.RegisterImporter(_newImporter);
	}

	/// <summary>
	/// Tries to register a new image or texture importer.
	/// </summary>
	/// <param name="_newImporter">The new importer instance.</param>
	/// <returns>True if the importer was registered successfully, false otherwise.</returns>
	public bool RegisterImageImporter(IImageImporter _newImporter)
	{
		return !IsDisposed && imageImporter.RegisterImporter(_newImporter);
	}

	#endregion
}
