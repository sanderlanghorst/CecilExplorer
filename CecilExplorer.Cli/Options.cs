using CommandLine;

namespace CecilExplorer.Cli;

public interface IOptions
{
    bool Verbose { get; set; }
    
    string Internal { get; set; }
    
    string Assembly { get; set; }
    
    string Output { get; set; }
    
    Level Level { get; set; }
}

public enum Level
{
    Module,
    Class,
    Method
}

[Verb("dump", true, HelpText = "Creates a dump of the assembly")]
public class DumpOptions : IOptions
{
    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }

    [Option('i', "internal", Required = false, HelpText = "Internal assembly names, comma separated, wildcard supported")]
    public string Internal { get; set; } = string.Empty;

    [Option('a', "assembly", Required = true, HelpText = "Assembly file")]
    public required string Assembly { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output file")]
    public string Output { get; set; } = string.Empty;

    [Option('t', "term", Required = false, HelpText = "Filter term")]
    public string Term { get; set; } = string.Empty;

    [Option('l', "level", Required = false, HelpText = "Level of detail")]
    public Level Level { get; set; } = Level.Class;
}

[Verb("trace", HelpText = "Trace references")]
public class TraceOptions : IOptions
{
    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }

    [Option('i', "internal", Required = false, HelpText = "Internal assembly names, comma separated, wildcard supported")]
    public string Internal { get; set; } = string.Empty;
    
    [Option('f', "from", Required = true, HelpText = "From type")]
    public required string FromType { get; set; }
    
    [Option('t', "to", Required = true, HelpText = "To type")]
    public required string ToType { get; set; }
    
    [Option('a', "assembly", Required = true, HelpText = "Assembly file")]
    public required string Assembly { get; set; }
    
    [Option('o', "output", Required = false, HelpText = "Output file")]
    public string Output { get; set; } = string.Empty;
    
    [Option('l', "level", Required = false, HelpText = "Level of detail")]
    public Level Level { get; set; } = Level.Class;
}