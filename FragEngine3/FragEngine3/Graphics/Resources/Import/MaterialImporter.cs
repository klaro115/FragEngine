using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Graphics.Resources.Import;

public static class MaterialImporter
{
	#region Methods

	public static bool ImportMaterialData(ResourceHandle _resourceHandle, Logger _logger, out MaterialData? _outMaterialData)
	{
		if (!BaseResourceImporter.TryGetResourceFile(_resourceHandle, _logger, out ResourceFileHandle fileHandle))
		{
			_outMaterialData = null;
			return false;
		}

		// Try reading raw byte data from file:
		if (!fileHandle.TryReadResourceBytes(_resourceHandle, out byte[] bytes, out int byteCount))
		{
			_logger.LogError($"Failed to read material JSON for resource '{_resourceHandle}'!");
			_outMaterialData = null;
			return false;
		}

		// Try converting byte data to string containing JSON-encoded material data:
		string jsonTxt;
		try
		{
			jsonTxt = System.Text.Encoding.UTF8.GetString(bytes, 0, byteCount);
		}
		catch (Exception ex)
		{
			_logger.LogException($"Failed to decode material JSON for resource '{_resourceHandle}'!", ex);
			_outMaterialData = null;
			return false;
		}

		// Try deserializing material description data from JSON:
		bool success = Serializer.DeserializeFromJson(jsonTxt, out _outMaterialData) && _outMaterialData is not null;
		return success;
	}

	#endregion
}
