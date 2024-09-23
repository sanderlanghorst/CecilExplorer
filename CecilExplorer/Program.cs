// See https://aka.ms/new-console-template for more information

using CecilExplorer;

var loader = new ModuleLoader();
await loader.Load(@"CecilExplorer.dll");

Console.WriteLine($"Loaded {loader.Types.Count} types");