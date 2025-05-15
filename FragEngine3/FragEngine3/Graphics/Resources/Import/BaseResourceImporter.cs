using FragEngine3.EngineCore;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

public abstract class BaseResourceImporter : IDisposable
{
	#region Constructors

	/// <summary>
	/// Gets whether this instance has been disposed.
	/// </summary>
	public bool IsDisposed { get; protected set; } = false;

	~BaseResourceImporter()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	protected abstract void Dispose(bool _disposing);

	internal static bool TryGetResourceFile(ResourceHandle _handle, Logger _logger, out ResourceFileHandle _outFileHandle)
	{
		if (_handle is null || !_handle.IsValid)
		{
			_logger.LogError("Resource handle for graphics resource import may not be null or invalid!");
			_outFileHandle = ResourceFileHandle.None;
			return false;
		}
		if (_handle.resourceManager is null || _handle.resourceManager.IsDisposed)
		{
			_logger.LogError("Cannot load graphics resource using null or disposed resource manager!");
			_outFileHandle = ResourceFileHandle.None;
			return false;
		}

		// Retrieve the file that this resource is loaded from:
		if (string.IsNullOrEmpty(_handle.fileKey))
		{
			if (!_handle.resourceManager.GetFileWithResource(_handle.fileKey, out _outFileHandle))
			{
				_logger.LogError($"Could not find any resource data file containing resource handle '{_handle}'!");
				_outFileHandle = ResourceFileHandle.None;
				return false;
			}
		}
		else
		{
			if (!_handle.resourceManager.GetFile(_handle.fileKey, out _outFileHandle))
			{
				_logger.LogError($"Resource data file for resource handle '{_handle}' does not exist!");
				_outFileHandle = ResourceFileHandle.None;
				return false;
			}
		}
		return _outFileHandle.IsValid;
	}

	#endregion
}

/// <summary>
/// Hub type for managing and delegating the import of graphics resources through previously registered importers.
/// Use '<see cref="RegisterImporter(TImporter)"/>' to add new types of importers with all the file formats they support.
/// </summary>
/// <typeparam name="TImporter">A interface type specific to a resource type.</typeparam>
public abstract class BaseResourceImporter<TImporter> : BaseResourceImporter
	where TImporter : IGraphicsResourceImporter
{
	#region Constructors

	/// <summary>
	/// Creates a new modle importer hub instance through which all 3D model formats may be routed to their respective importers.
	/// </summary>
	/// <param name="_resourceManager">The engine's resource manager instance.</param>
	/// <param name="_graphicsCore">The graphics core through which's graphics device the 3D model resources shall be created.</param>
	protected BaseResourceImporter(ResourceManager _resourceManager, GraphicsCore _graphicsCore)
	{
		resourceManager = _resourceManager;
		graphicsCore = _graphicsCore;
		logger = resourceManager.Engine.Logger ?? Logger.Instance!;

		importCtx = new()
		{
			Logger = logger,
			JsonOptions = null,
		};
	}

	#endregion
	#region Fields

	protected readonly ResourceManager resourceManager;
	protected readonly GraphicsCore graphicsCore;
	protected readonly Logger logger;

	protected ImporterContext importCtx;

	protected readonly Dictionary<string, TImporter> importerFormatDict = [];
	protected readonly List<TImporter> importers = [];

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets context information for model import. Only non-null and valid values are accepted.
	/// </summary>
	public ImporterContext ImportCtx
	{
		get => importCtx;
		set
		{
			if (value is not null && value.IsValid())
			{
				importCtx = value;
			}
		}
	}

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		IsDisposed = true;
		foreach (TImporter importer in importers)
		{
			if (importer is IDisposable importerDisposable)
			{
				importerDisposable.Dispose();
			}
		}
		importerFormatDict.Clear();
		importers.Clear();
	}

	/// <summary>
	/// Registers a new type of importer.
	/// </summary>
	/// <param name="_newImporter">The new resource importer instance. Upon successful addition, ownership of the importer is passed to this instance.
	/// If the importer instance implements the <see cref="IDisposable"/> interface, it will be disposed safely once this object expires.</param>
	/// <returns>True if the model importer was registered for importing of at least one file format.</returns>
	public virtual bool RegisterImporter(TImporter _newImporter)
	{
		if (_newImporter is null)
		{
			logger.LogError($"Cannot register null importer of type '{typeof(TImporter).Name}'!");
			return false;
		}
		if (importers.Any(o => o.GetType() == _newImporter.GetType()))
		{
			logger.LogError($"Cannot register importer '{_newImporter}' multiple times!");
			return false;
		}

		bool wasAdded = false;

		IReadOnlyCollection<string> fileExtensions = _newImporter.GetSupportedFileFormatExtensions();
		foreach (string fileExt in fileExtensions)
		{
			wasAdded |= importerFormatDict.TryAdd(fileExt, _newImporter);
		}

		if (!wasAdded)
		{
			logger.LogWarning($"Discarding importer '{_newImporter}'; all its supported file formats are already covered by other importers.");
		}
		return wasAdded;
	}

	protected bool TryGetResourceFile(ResourceHandle _handle, out ResourceFileHandle _outFileHandle)
	{
		bool success = TryGetResourceFile(_handle, logger, out _outFileHandle);
		return success;
	}

	#endregion
}
