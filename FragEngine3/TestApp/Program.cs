using FragAssetFormats.Geometry.OBJ;
using FragAssetFormats.Shaders.FSHA;
using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Resources.Import.ModelFormats;
using TestApp.Application;

Console.WriteLine("### Starting...\n");

EngineConfig config = new();
//config.Graphics.PreferNativeFramework = false;	// default to Vulkan
//config.Graphics.CenterWindowOnScreen = false;

Engine? engine = null;
try
{
	engine = new(new TestApplicationLogic(), config);
	//engine = new(new TestEmptyAppLogic(), config);

	// Register services and importers:
	{
		// 3D formats:
		engine.GraphicsResourceLoader.RegisterModelImporter(new ObjImporter());
		engine.GraphicsResourceLoader.RegisterModelImporter(new FbxImporter());

		// Shader formats:
		engine.GraphicsResourceLoader.RegisterShaderImporter(new FshaImporter());
	}

	engine.Run();
}
catch (Exception ex)
{
	Console.WriteLine($"ERROR! Engine crashed due to an unhandled exception!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'\nException trace: {ex.StackTrace ?? "NULL"}");
}
finally
{
	engine?.Dispose();
}
