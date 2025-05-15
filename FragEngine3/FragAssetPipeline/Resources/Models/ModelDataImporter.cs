using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;

namespace FragAssetPipeline.Resources.Models;

internal sealed class ModelDataImporter(ImporterContext _importCtx) : IDisposable
{
	#region Constructors

	~ModelDataImporter()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly ImporterContext importCtx = _importCtx ?? throw new ArgumentNullException(nameof(_importCtx), "Importer context may not be null!");

	private readonly Dictionary<string, IModelImporter> importerFormatDict = [];
	private readonly List<IModelImporter> importers = [];
	
	#endregion
	#region Properties

	/// <summary>
	/// Gets whether this instance has been disposed.
	/// </summary>
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
		foreach (IModelImporter importer in importers)
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
	/// <param name="_newImporter">The new model importer instance. Upon successful addition, ownership of the importer is passed to this instance.
	/// If the importer instance implements the <see cref="IDisposable"/> interface, it will be disposed safely once this object expires.</param>
	/// <returns>True if the model importer was registered for importing of at least one file format.</returns>
	public bool RegisterImporter(IModelImporter _newImporter)
	{
		if (_newImporter is null)
		{
			importCtx.Logger.LogError($"Cannot register null model importer!");
			return false;
		}
		if (importers.Any(o => o.GetType() == _newImporter.GetType()))
		{
			importCtx.Logger.LogError($"Cannot register model importer '{_newImporter}' multiple times!");
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
			importCtx.Logger.LogWarning($"Discarding model importer '{_newImporter}'; all its supported file formats are already covered by other importers.");
		}
		return wasAdded;
	}

	public bool ImportModelData(
		string _dataFilePath,
		string _resourceKeyBase,
		string? _importFlags,
		out Dictionary<string, MeshSurfaceData>? _outSurfaceData
		/* out ... */)
	{
		if (IsDisposed)
		{
			importCtx.Logger.LogError($"Cannot import model data using disposed {nameof(ModelDataImporter)}!");
			_outSurfaceData = null;
			return false;
		}
		if (string.IsNullOrEmpty(_dataFilePath))
		{
			importCtx.Logger.LogError("Cannot import model data from null or blank file path!");
			_outSurfaceData = null;
			return false;
		}
		if (!File.Exists(_dataFilePath))
		{
			importCtx.Logger.LogError($"Cannot import model data; data file does not exist! File path: '{_dataFilePath}'");
			_outSurfaceData = null;
			return false;
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			stream = new FileStream(_dataFilePath, FileMode.Open, FileAccess.Read);

			// Import from stream, identifying file format from extension:
			string formatExt = Path.GetExtension(_dataFilePath);

			if (!ImportModelData(stream, _resourceKeyBase, formatExt, out _outSurfaceData))
			{
				importCtx.Logger.LogError($"Failed to import model data for data file '{_dataFilePath}'!");
				_outSurfaceData = null;
				return false;
			}
		}
		catch (Exception ex)
		{
			importCtx.Logger.LogException($"Failed to import model data for data file '{_dataFilePath}'!", ex);
			_outSurfaceData = null;
			return false;
		}
		finally
		{
			stream?.Close();
		}

		// Check for further pre-processing instructions in import flags:
		if (_outSurfaceData is not null && !string.IsNullOrEmpty(_importFlags))
		{
			// Flip triangle vertex order, optinally flip normals and tangents: (turns the surfaces inside-out)
			if (_importFlags.Contains(ImportFlagsConstants.MOD_FLIP_VERTEX_ORDER, StringComparison.Ordinal))
			{
				bool flipNormals = _importFlags.Contains(ImportFlagsConstants.MOD_FLIP_NORMALS, StringComparison.Ordinal);
				bool flipTangents = _importFlags.Contains(ImportFlagsConstants.MOD_FLIP_TANGENTS, StringComparison.Ordinal);

				foreach (var kvp in _outSurfaceData)
				{
					MeshSurfaceData surfaceData = kvp.Value;
					surfaceData.ReverseVertexOrder(flipNormals, flipTangents);
				}
			}
		}
		return true;
	}

	private bool ImportModelData(
		Stream _stream,
		string _resourceKeyBase,
		string _formatExt,
		out Dictionary<string, MeshSurfaceData>? _outSurfaceData
		/* out ... */)
	{
		if (string.IsNullOrWhiteSpace(_formatExt))
		{
			importCtx.Logger.LogError("Cannot import model data using unspecified 3D file format extension!");
			_outSurfaceData = null;
			return false;
		}

		_formatExt = _formatExt.ToLowerInvariant();

		if (!importerFormatDict.TryGetValue(_formatExt, out IModelImporter? importer))
		{
			importCtx.Logger.LogError($"Unsupported 3D file format extension '{_formatExt}', cannot import model data!");
			_outSurfaceData = null;
			return false;
		}

		bool success = true;
		_outSurfaceData = [];

		IEnumerator<string> e = importer.EnumerateSubresources(importCtx, _stream, _resourceKeyBase, _formatExt);
		while (e.MoveNext())
		{
			_stream.Position = 0;
			bool result = importer.ImportSurfaceData(in importCtx, _stream, e.Current, out MeshSurfaceData? surfaceData, _formatExt);
			if (result)
			{
				_outSurfaceData.Add(e.Current, surfaceData!);
			}
			else
			{
				importCtx.Logger.LogWarning($"Failed to import surface data of sub-mesh '{e.Current}'!");
			}
			success &= result;
		}

		return success;
	}

	#endregion
}
