// See https://aka.ms/new-console-template for more information
using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.EngineCore.Test;

Console.WriteLine("### Starting...\n");

EngineConfig config = new();

using Engine engine = new(new TestApplicationLogic(), config);

engine.Run();

engine.Dispose();
