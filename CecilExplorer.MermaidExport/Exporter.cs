using System.Text;

namespace CecilExplorer.MermaidExport;

public class Exporter
{
    private readonly string _path;
    private readonly DetailLevel _detailLevel;

    private static readonly char[] ValidChars =
    {
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
        'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
        'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '-'
    };

    public Exporter(string path, DetailLevel detailLevel)
    {
        _path = path;
        _detailLevel = detailLevel;
    }

    public long SaveToFile(Workspace workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        ExportFlowChart(sb, workspace);
        sb.AppendLine("```");
        File.WriteAllText(_path, sb.ToString());
        return sb.Length;
    }

    private static string Sanitize(string? name)
    {
        return name is null 
            ? string.Empty 
            : new string(name.Where(ValidChars.Contains).ToArray());
    }

    private void ExportFlowChart(StringBuilder sb, Workspace workspace)
    {
        // create a mermaid class diagram
        sb.AppendLine("flowchart LR");
        foreach (var moduleGroup in workspace.References
                     .Where(r => SelectReference(r))
                     .OrderBy(r => r.FromType.FullName)
                     .SelectMany(r => new []
                     {
                         (module: r.FromType.Module.Name, @class: r.FromType.FullName, method: r.FromName),
                         (module: r.ToType.Module?.Name ?? "tbd", @class: r.ToType.FullName, method: r.ToName)
                     })
                     .GroupBy(r => (r.module, r.@class, r.method))
                     .GroupBy(r => (r.Key.module, r.Key.@class))
                     .GroupBy(r => r.Key.module))
        {
            int indent = 1;
            if (_detailLevel == DetailLevel.Module)
            {
                sb.AppendLine($"{Indent(indent)}{Sanitize(moduleGroup.Key)}[\"{moduleGroup.Key}\"]");
                continue;
            }

            sb.AppendLine($"{Indent(indent++)}subgraph {Sanitize(moduleGroup.Key)} [\"{moduleGroup.Key}\"]");

            foreach (var classGroup in moduleGroup)
            {
                if (_detailLevel == DetailLevel.Class)
                {
                    sb.AppendLine($"{Indent(indent)}{Sanitize(classGroup.Key.@class)}[\"{classGroup.Key.@class}\"]");
                    continue;
                }

                sb.AppendLine($"{Indent(indent++)}subgraph {Sanitize(classGroup.Key.@class)}_c[\"{classGroup.Key.@class}\"]");

                foreach (var methodGroup in classGroup)
                {
                    sb.AppendLine($"{Indent(indent)}{Sanitize(methodGroup.Key.method)}[\"{methodGroup.Key.method}\"]");
                }

                sb.AppendLine($"{Indent(indent--)}end");
            }

            sb.AppendLine($"{Indent(indent--)}end");
        }

        try
        {
            foreach (var tref in workspace.References
                         .Where(r => SelectReference(r))
                         .GroupBy(SelectReferenceGroupKey))
            {
                sb.Append("\t");

                sb.Append(Sanitize(tref.Key.from));
                sb.Append(" --> ");
                sb.AppendLine(Sanitize(tref.Key.to));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        string Indent(int i) => string.Join(string.Empty, Enumerable.Repeat(0, i).Select(_ => '\t'));
    }

    private (string from, string to) SelectReferenceGroupKey(Reference reference)
    {
        switch (_detailLevel)
        {
            case DetailLevel.Module:
                return (reference.FromType.Module.Name, reference.ToType.Module?.Name ?? "tbd");

            case DetailLevel.Class:
                return (reference.FromType.FullName, reference.ToType.FullName);

            case DetailLevel.Method:
                return (reference.FromName, reference.ToName);

            default:
                return (string.Empty, string.Empty);
        }
    }

    private bool SelectReference(Reference reference)
    {
        return (_detailLevel > DetailLevel.Module || !reference.FromType.Module.Equals(reference.ToType.Module))
               && (_detailLevel > DetailLevel.Class || !reference.FromType.Equals(reference.ToType));
    }

    private void ExportClassDiagram(StringBuilder sb, ModuleLoader loader)
    {
        // create a mermaid class diagram
        sb.AppendLine("classDiagram");
        foreach (var type in loader.References.Select(r => r.FromType).Distinct())
        {
            sb.AppendLine($"\tclass {Sanitize(type.FullName)} [\"{type.FullName}\"] {{");

            sb.AppendLine("\t}");
        }

        foreach (var t in loader.References.Where(r => loader.References.Any(or => r.FromType.BaseType == or.FromType))
                     .Distinct())
        {
            sb.AppendLine($"\t{Sanitize(t.FromType.BaseType.FullName)} <|-- {Sanitize(t.FromType.FullName)}");
        }

        foreach (var tref in loader.References.Where(r => !r.ToType.Module.Assembly.IsSystemLibrary())
                     .GroupBy(r => new { r.FromType, r.ToType }))
        {
            sb.AppendLine("\t" + Sanitize(tref.Key.FromType.FullName) + " --> " + Sanitize(tref.Key.ToType.FullName) +
                          " : " + string.Join(", ", tref.Select(r => r.ToName)));
        }
    }
}