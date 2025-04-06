using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

public sealed class MaterialImporter : BaseResourceImporter<IMaterialImporter>
{
	#region Constructors

	public MaterialImporter(ResourceManager _resourceManager, GraphicsCore _graphicsCore) : base(_resourceManager, _graphicsCore)
	{
		// Ensure that the default material importer is always registered right away, and used as fallback:
		fallbackImporter = new DefaultMaterialImporter();
		RegisterImporter(fallbackImporter);
	}

	~MaterialImporter()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly Dictionary<string, IMaterialImporter> importerTypeDict = [];

	private readonly IMaterialImporter fallbackImporter;

	#endregion
	#region Methods

	public override bool RegisterImporter(IMaterialImporter _newImporter)
	{
		bool success = base.RegisterImporter(_newImporter);
		if (!success)
		{
			return false;
		}

		// Register importer as prefered option for its supported material types: (unless they are already covered by a previously registered importer)
		IReadOnlyDictionary<string, Type> supportedMaterialTypes = _newImporter.GetSupportedMaterialTypes();

		foreach (var kvp in supportedMaterialTypes)
		{
			importerTypeDict.TryAdd(kvp.Key, _newImporter);
		}
		return true;
	}

	public bool ImportMaterialData(ResourceHandle _resourceHandle, out MaterialDataNew? _outMaterialData)
	{
		if (IsDisposed)
		{
			logger.LogError($"Cannot import material data using disposed {nameof(ModelImporter)}!");
			_outMaterialData = null;
			return false;
		}

		if (!TryGetResourceFile(_resourceHandle, out ResourceFileHandle fileHandle))
		{
			_outMaterialData = null;
			return false;
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			if (!fileHandle.TryOpenDataStream(resourceManager.engine, _resourceHandle.dataOffset, _resourceHandle.dataSize, out stream, out _))
			{
				logger.LogError($"Failed to open file stream for resource handle '{_resourceHandle}'!");
				_outMaterialData = null;
				return false;
			}

			// Import from stream, identifying file format from extension:
			string formatExt = Path.GetExtension(fileHandle.dataFilePath);

			if (!ImportMaterialData(stream, formatExt, out _outMaterialData))
			{
				logger.LogError($"Failed to import material data for resource handle '{_resourceHandle}'!");
				_outMaterialData = null;
				return false;
			}
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to import material data for resource handle '{_resourceHandle}'!", ex);
			_outMaterialData = null;
			return false;
		}
		finally
		{
			stream?.Close();
		}

		return true;
	}

	public bool ImportMaterialData(Stream _stream, string _formatExt, out MaterialDataNew? _outMaterialData)
	{
		if (_stream is null || !_stream.CanRead)
		{
			logger.LogError("Cannot import model data from null or write-only stream!");
			_outMaterialData = null;
			return false;
		}
		if (string.IsNullOrWhiteSpace(_formatExt))
		{
			logger.LogError("Cannot import model data using unspecified 3D file format extension!");
			_outMaterialData = null;
			return false;
		}

		_formatExt = _formatExt.ToLowerInvariant();

		if (!importerFormatDict.TryGetValue(_formatExt, out IMaterialImporter? importer))
		{
			logger.LogWarning($"Possible unsupported material file format extension '{_formatExt}', trying fallback importer instead.");
			importer = fallbackImporter;
		}

		bool success = importer.ImportMaterialData(in importCtx, _stream, out _outMaterialData);
		return success;
	}

	public bool CreateMaterial(in ResourceHandle _handle, in MaterialDataNew _materialData, out Material? _outMaterial)
	{
		// Check input parameters:
		if (_handle is null || !_handle.IsValid)
		{
			logger.LogError("Resource handle for material creation may not be null or invalid!");
			_outMaterial = null;
			return false;
		}
		if (_materialData is null || !_materialData.IsValid())
		{
			logger.LogError("Cannot create material resource data using null or invalid material data!");
			_outMaterial = null;
			return false;
		}

		// Figure out which importer to use for the specified material type:
		string typeName = !string.IsNullOrEmpty(_materialData.TypeName)
			? _materialData.TypeName
			: nameof(DefaultSurfaceMaterial);

		if (!importerTypeDict.TryGetValue(typeName, out IMaterialImporter? importer))
		{
			logger.LogWarning($"Possible unsupported material type '{typeName}', trying fallback importer instead.");
			importer = fallbackImporter;
		}

		// Try to create material resource via the importer:
		bool success = importer.CreateMaterial(in _handle, in graphicsCore, in _materialData, out _outMaterial);
		return success;
	}

	#endregion
}
