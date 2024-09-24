using System.Text;
using CecilExplorer;

namespace CecilExplorer.MermaidExport;

public class Exporter
{
    private readonly string _path;

    public Exporter(string path)
    {
        _path = path;
    }
    public void SaveToFile(ModuleLoader loader)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        ExportFlowChart(sb, loader);
        sb.AppendLine("```");
        File.WriteAllText(_path, sb.ToString());
    }
    
    private static char[] ValidChars = new []{'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '-'};
    private static string Sanitize(string name)
    {
        return new string(name.Where(ValidChars.Contains).ToArray());
    }
    private void ExportFlowChart(StringBuilder sb, ModuleLoader loader)
    {
        // create a mermaid class diagram
        sb.AppendLine("flowchart LR");
        foreach(var moduleGroup in loader.References.Select(r => r.FromType).GroupBy(r => r.Module.Name))
        {
            sb.AppendLine($"\tsubgraph {moduleGroup.Key}");
            foreach (var type in moduleGroup.Distinct())
            {
                sb.AppendLine($"\t\t{Sanitize(type.FullName)}[\"{type.FullName}\"]");                
            }
            
            sb.AppendLine("\tend");
        }

        foreach (var tref in loader.References.Where(r => !r.ToType.Module.Assembly.IsSystemLibrary()).GroupBy(r => new {r.FromType, r.ToType}))
        {
            sb.Append("\t");
            sb.Append(Sanitize(tref.Key.FromType.FullName));
            sb.Append(" -- ");
            sb.Append(string.Join(", ", tref.Select(r => r.ToName).Distinct()));
            sb.Append(" --> ");
            sb.AppendLine(Sanitize(tref.Key.ToType.FullName));
        }
    }
    private void ExportClassDiagram(StringBuilder sb, ModuleLoader loader)
    {
        // create a mermaid class diagram
        sb.AppendLine("classDiagram");
        foreach(var type in loader.References.Select(r => r.FromType).Distinct())
        {
            sb.AppendLine($"\tclass {Sanitize(type.FullName)} [\"{type.FullName}\"] {{");
            
            sb.AppendLine("\t}");
        }

        foreach (var t in loader.References.Where(r => loader.References.Any(or => r.FromType.BaseType == or.FromType)).Distinct())
        {
            sb.AppendLine($"\t{Sanitize(t.FromType.BaseType.FullName)} <|-- {Sanitize(t.FromType.FullName)}");
        }

        foreach (var tref in loader.References.Where(r => !r.ToType.Module.Assembly.IsSystemLibrary()).GroupBy(r => new {r.FromType, r.ToType}))
        {
            sb.AppendLine("\t" + Sanitize(tref.Key.FromType.FullName) + " --> " + Sanitize(tref.Key.ToType.FullName) + " : " + string.Join(", ", tref.Select(r => r.ToName)));
        }
    }
}