using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats;

public static class FbxImporter
{
	#region Methods

	public static bool ImportModel(Stream _streamBytes, out MeshSurfaceData? _outMeshData)
	{
		if (_streamBytes is null || !_streamBytes.CanRead)
		{
			Logger.Instance?.LogError("Cannot import FBX document from null or write-only stream!");
			_outMeshData = null;
			return false;
		}

		using BinaryReader reader = new(_streamBytes);

		if (FbxDocument.ReadFbxDocument(reader, out FbxDocument? document))
		{
			Logger.Instance?.LogError("Failed to import FBX document, aborting model import!");
			_outMeshData = null;
			return false;
		}

		//TODO

		_outMeshData = null;	//TEMP
		return true;
	}

	#endregion
}
