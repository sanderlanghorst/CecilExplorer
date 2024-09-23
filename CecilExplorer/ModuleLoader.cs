using System.Collections.Concurrent;
using Mono.Cecil;

namespace CecilExplorer;

public class ModuleLoader
{
    public ModuleLoader()
    {
        //options
    }

    private List<AssemblyDefinition> _loadedAssemblies = [];
    private ConcurrentQueue<ModuleReference> _moduleReferences = [];

    public List<TypeDefinition> Types = [];
    
    public Task Load(string file)
    {
        var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(file);
        _loadedAssemblies.Add(assembly);

        var modules = assembly.Modules;
        foreach (var module in modules)
        {
            foreach (var type in module.GetTypes())
            {
                Types.Add(type);
            }
        }

        return Task.CompletedTask;
    }
}