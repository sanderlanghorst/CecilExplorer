// See https://aka.ms/new-console-template for more information

using CecilExplorer;
using CecilExplorer.Cli;
using CecilExplorer.MermaidExport;
using CommandLine;
using TraceOptions = CecilExplorer.Cli.TraceOptions;

await Parser.Default.ParseArguments<DumpOptions, TraceOptions>(args)
    .MapResult<DumpOptions, TraceOptions,Task>(
        dump => LoadAssembly(dump),
        trace => LoadAssembly(trace),
        _ =>
        {
            Console.Error.WriteLine("Invalid arguments");
            return Task.CompletedTask;
        });
    
async Task LoadAssembly(IOptions o){
    var loader = new ModuleLoader(o.Assembly, o.Internal);
    await loader.Load();
    var exporter = new Exporter(o.Output, o.Term, o.Level switch {
        Level.Module => DetailLevel.Module, Level.Class => DetailLevel.Class, Level.Method => DetailLevel.Method,
        _ => DetailLevel.Class
    });
    exporter.SaveToFile(loader);
    Console.WriteLine($"Loaded {loader.Modules.Count} modules {loader.Types.Count} types");
}
