using System.Collections.Frozen;
using System.Text.Json;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

public class DefaultMaterialImporter : IMaterialImporter
{
	#region Fields

	private static readonly string[] supportedFileExtensions = [".json"];

	private static readonly FrozenDictionary<string, Type> supportedMaterialTypes = new Dictionary<string, Type>()
	{
		[typeof(DefaultSurfaceMaterial).Name] = typeof(DefaultSurfaceMaterial),
	}.ToFrozenDictionary();

	#endregion
	#region Properties

	public bool CanImportDefaultMaterials => true;

	public bool CanImportSurfaceMaterials => true;

	public bool CanImportComputeMaterials => true;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFileExtensions;

	public IReadOnlyDictionary<string, Type> GetSupportedMaterialTypes() => supportedMaterialTypes;

	public bool ImportMaterialData(in ImporterContext _importCtx, Stream _resourceFileStream, out MaterialDataNew? _outMaterialData)
	{
		try
		{
			_outMaterialData = JsonSerializer.Deserialize<MaterialDataNew>(_resourceFileStream, _importCtx.JsonOptions);
			return _outMaterialData is not null;
		}
		catch (Exception ex)
		{
			_importCtx.Logger.LogException($"Failed to decode material JSON from resource file stream!", ex);
			_outMaterialData = null;
			return false;
		}
	}

	public bool CreateMaterial(in ResourceHandle _resourceHandle, in GraphicsCore _graphicsCore, in MaterialDataNew _materialData, out MaterialNew? _outMaterial)
	{
		try
		{
			_outMaterial = new DefaultSurfaceMaterial(_graphicsCore, _resourceHandle, _materialData);
			return true;
		}
		catch (Exception ex)
		{
			_graphicsCore.graphicsSystem.Engine.Logger.LogException($"Failed to create new default surface material for resource '{_resourceHandle.resourceKey}'!", ex);
			_outMaterial = null;
			return false;
		}
	}

	public IEnumerator<ResourceHandle> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream)
	{
		throw new NotImplementedException();
	}

	#endregion
}
