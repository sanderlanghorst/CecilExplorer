namespace CecilExplorer;

using Mono.Cecil;

public static class AssemblyExtensions
{
    private const string System = "System";
    private static readonly string[] SystemAssemblies =
    {
        "7cec85d7bea7798e", //System.Private.CoreLib
        "b03f5f7f11d50a3a", //System.Console, System.Linq
        "50cebf1cceb9d05e", //Mono.Cecil
        "31bf3856ad364e35", //System.Web
    };

    public static bool IsSystemReference(this TypeReference typeReference)
    {
        return typeReference.FullName.StartsWith(System);
    }
    
    public static bool IsSystemLibrary(this AssemblyDefinition assembly)
    {
        return assembly.FullName.StartsWith(System) || SystemAssemblies.Any(assembly.FullName.Contains);
    }
    public static bool IsSystemLibrary(this IMetadataScope scope)
    {
        return scope is AssemblyNameReference assemblyNameReference 
               && (assemblyNameReference.FullName.StartsWith(System) || 
               SystemAssemblies.Any(assemblyNameReference.FullName.Contains));
    }

    public static string AssemblyName(this TypeReference type)
    {
        return type.Scope switch
        {
            AssemblyNameReference assemblyNameReference => assemblyNameReference.FullName,
            ModuleDefinition moduleDefinition => moduleDefinition.Assembly.FullName,
            _ => string.Empty
        };
    }
}