namespace CecilExplorer;

using Mono.Cecil;

public static class AssemblyExtensions
{
    private static readonly string[] SystemAssemblies =
    {
        "7cec85d7bea7798e", //System.Private.CoreLib
        "b03f5f7f11d50a3a", //System.Console, System.Linq
        "50cebf1cceb9d05e", //Mono.Cecil
    };
    public static bool IsSystemLibrary(this AssemblyDefinition assembly)
    {
        return SystemAssemblies.Any(assembly.FullName.Contains);
    }
    public static bool IsSystemLibrary(this IMetadataScope scope)
    {
        return scope is AssemblyNameReference assemblyNameReference 
               && SystemAssemblies.Any(assemblyNameReference.FullName.Contains);
    }
}