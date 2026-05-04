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
    Console.WriteLine($"Loading {o.Assembly}...");
    var loader = new ModuleLoader(o.Assembly, o.Internal);
    var workspace = await loader.Load();
    Console.WriteLine($"Loaded {workspace.Assemblies.Length} assemblies {workspace.Types.Length} types.");
    
    IFilter filter = o switch { DumpOptions d => new SimpleFilter(d.Term), TraceOptions t => new TraceFilter(t.FromType, t.ToType), _ => new SimpleFilter(string.Empty) };
    var detailLevel = o.Level switch
    {
        Level.Module => DetailLevel.Module, Level.Class => DetailLevel.Class, Level.Method => DetailLevel.Method,
        _ => DetailLevel.Class
    };
    var filteredWorkspace = filter.Filter(workspace);
    Console.WriteLine($"Filtered to {filteredWorkspace.Assemblies.Length} assemblies {filteredWorkspace.Types.Length} types.");
    
    var exporter = new Exporter(o.Output, detailLevel);
    var @out = exporter.SaveToFile(filteredWorkspace);
    Console.WriteLine($"Written file to {Path.GetFullPath(o.Output)} with {@out} characters.");
}
