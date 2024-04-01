using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats;

public static class FbxImporter
{
	#region Methods

	public static bool ImportModel(Stream _streamBytes, out MeshSurfaceData? _outMeshData)
	{
		if (!ImportFbxDocument(_streamBytes, out FbxDocument? document))
		{
			Logger.Instance?.LogError("Failed to import FBX document, aborting model import!");
			_outMeshData = null;
			return false;
		}

		//TODO

		_outMeshData = null;
		return false;			//TEMP
	}

	public static bool ImportFbxDocument(Stream _streamBytes, out FbxDocument? _outDocument)
	{
		//TODO

		_outDocument = null;
		return false;
	}

	#endregion
}
