using System.Collections.Immutable;
using Mono.Cecil;

namespace CecilExplorer;

public class Workspace
{
    public Workspace(ICollection<AssemblyDefinition> assemblies, ICollection<TypeDefinition> types, ICollection<Reference> references)
    {
        Assemblies = [..assemblies];
        Types = [..types];
        References = [..references];
    }

    public ImmutableArray<AssemblyDefinition> Assemblies { get; }
    public ImmutableArray<TypeDefinition> Types { get; }
    public ImmutableArray<Reference> References { get; }
}