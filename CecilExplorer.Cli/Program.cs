// See https://aka.ms/new-console-template for more information

using CecilExplorer;
using CecilExplorer.MermaidExport;

var loader = new ModuleLoader();
await loader.Load(@"CecilExplorer.Cli.dll");
var exporter = new Exporter("out.md");
exporter.SaveToFile(loader);
Console.WriteLine($"Loaded {loader.Modules.Count} modules {loader.Types.Count} types");