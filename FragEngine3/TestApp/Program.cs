using FragAssetFormats.Extensions;
using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using TestApp.Application;

Console.WriteLine("### Starting...\n");

EngineConfig config = new()
{
	ApplicationName = "TestApp",
	MainWindowTitle = "TestApp MainWindow",

	Logger = new()
	{
		DeletePreviousLogsOnStartup = true,
	},
	Graphics = new()
	{
		PreferNativeFramework = true,
		CenterWindowOnScreen = true,
		FallbackGraphicsSettings = new()
		{
			Resolution = new(1280, 720),
			MaxFrameRate = 120,
		},
	},
};

Engine? engine = null;
try
{
	engine = new(new TestApplicationLogic(), config);
	//engine = new(new TestEmptyAppLogic(), config);

	// Register services and importers:
	engine.RegisterAssetFormatImporters();

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
