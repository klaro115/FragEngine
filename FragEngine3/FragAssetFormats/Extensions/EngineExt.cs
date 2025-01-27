using FragAssetFormats.Geometry.OBJ;
using FragAssetFormats.Geometry;
using FragAssetFormats.Images;
using FragAssetFormats.Shaders.FSHA;
using FragEngine3.EngineCore;

namespace FragAssetFormats.Extensions;

/// <summary>
/// Helper class with extension methods for the <see cref="Engine"/> class.
/// </summary>
public static class EngineExt
{
	#region Methods

	/// <summary>
	/// Registers all asset importers in the "FragAssetFormats" library with the engine's graphics resource loaders.
	/// </summary>
	/// <param name="_engine">The engine whose asset support we wish to extend with additional importers.</param>
	/// <returns>True if all importers were registered successfully, false otherwise.</returns>
	public static bool RegisterAssetFormatImporters(this Engine _engine)
	{
		if (_engine is null || _engine.IsDisposed)
		{
			return false;
		}

		bool success = true;

		// 3D formats:
		success &= _engine.GraphicsResourceLoader.RegisterModelImporter(new ObjImporter());
		success &= _engine.GraphicsResourceLoader.RegisterModelImporter(new FbxImporter());

		// Shader formats:
		success &= _engine.GraphicsResourceLoader.RegisterShaderImporter(new FshaImporter());

		// Image formats:
		success &= _engine.GraphicsResourceLoader.RegisterImageImporter(new BitmapImporter());
		success &= _engine.GraphicsResourceLoader.RegisterImageImporter(new QoiImporter());
		//...
		success &= _engine.GraphicsResourceLoader.RegisterImageImporter(new MagickImporter()); //fallback

		return success;
	}

	#endregion
}
