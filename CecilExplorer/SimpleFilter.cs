using System.Text.RegularExpressions;
using Mono.Cecil;

namespace CecilExplorer;

public interface IFilter
{
    Workspace Filter(Workspace workspace);
}
public class SimpleFilter : IFilter
{
    private readonly Regex _pattern;

    public SimpleFilter(string patternString)
    {
        _pattern = PatternHelper.GetPattern(patternString);
    }
    
    public Workspace Filter(Workspace workspace)
    {
        var filteredTypes = workspace.Types.Where(t => SelectType(t) || !IsSystemLibrary(t)).ToArray();
        var filteredReferences = workspace.References.Where(r => SelectReference(r) && !IsSystemLibrary(r.ToType)).ToArray();
        var assemblies = filteredTypes.Select(f => f.Module.Assembly).Distinct().ToArray();
        return new Workspace(assemblies, filteredTypes, filteredReferences);
    }
    
    private bool IsSystemLibrary(TypeDefinition toType)
    {
        return toType.Module == null ||
               toType.Module.Assembly == null ||
               toType.Module.Assembly.IsSystemLibrary();
    }

    private bool SelectType(TypeDefinition type)
    {
        return _pattern.IsMatch(type.FullName);
    }

    private bool SelectReference(Reference reference)
    {
        return _pattern.IsMatch(reference.FromType.FullName) || _pattern.IsMatch(reference.ToType.FullName);
    }
}