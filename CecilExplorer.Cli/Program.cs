// See https://aka.ms/new-console-template for more information

using CecilExplorer;

var loader = new ModuleLoader();
await loader.Load(@"CecilExplorer.Cli.dll");

Console.WriteLine($"Loaded {loader.Modules.Count} modules {loader.Types.Count} types");