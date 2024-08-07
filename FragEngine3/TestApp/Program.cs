﻿using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using TestApp.Application.Test;

Console.WriteLine("### Starting...\n");

EngineConfig config = new();

Engine? engine = null;
try
{
	engine = new(new TestApplicationLogic(), config);
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
