using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Scenes;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackComposition(Logger _logger) : IDisposable
{
	#region Constructors

	~DefaultStackComposition()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly Logger logger = _logger;

	private bool isInitialized = false;

	private StaticMeshRendererComponent? compositionRenderer = null;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized => !IsDisposed && isInitialized;

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
		if (isInitialized)
		{
			Shutdown();
		}
	}

	public bool Initialize(Scene _scene)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot initialize composition for default graphics that has already been disposed!");
			return false;
		}
		if (IsInitialized)
		{
			logger.LogError("Cannot re-initialize composition for default graphics; module is already initialized!");
			return false;
		}
		if (_scene is null || _scene.IsDisposed || _scene.rootNode.IsDisposed)
		{
			logger.LogError("Cannot initialize composition for default graphics stack of null or disposed scene!");
			return false;
		}

		//TODO 1: Create fullscreen quad mesh
		//TODO 2: Load composition materials
		//TODO 3: Create renderer(s)
		//TODO 4: Register listeners for lifecycle events of renderer components

		isInitialized = true;
		return true;
	}

	public void Shutdown()
	{
		isInitialized = false;

		if (compositionRenderer is not null)
		{
			compositionRenderer.node.DestroyNode();
			compositionRenderer = null;
		}
		//...
	}

	public bool CompositeOutput()
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot composite rendering output of default graphics stack using uninitialized composition module!");
			return false;
		}

		//TODO

		return true;    //TEMP
	}

	#endregion
}
