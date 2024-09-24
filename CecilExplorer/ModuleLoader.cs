using System.Collections.Concurrent;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CecilExplorer;

public class ModuleLoader
{
    private readonly List<AssemblyDefinition> _loadedAssemblies = [];
    private ConcurrentQueue<ModuleReference> _assembliesToImport = [];
    private readonly ConcurrentQueue<TypeReference> _typesToImport = [];

    public List<TypeDefinition> Types = [];
    public List<Reference> References = [];
    public List<ModuleDefinition> Modules = [];

    public Task Load(string file)
    {
        var assembly = AssemblyDefinition.ReadAssembly(file);
        _loadedAssemblies.Add(assembly);

        var modules = assembly.Modules;
        foreach (var module in modules)
        {
            Modules.Add(module);
            foreach (var type in module.GetTypes())
            {
                _typesToImport.Enqueue(type);
            }
        }

        return StartLoading();
    }

    private Task StartLoading()
    {
        while (_typesToImport.TryDequeue(out var typeReference))
        {
            if (Types.Contains(typeReference))
            {
                continue;
            }

            if (!Modules.Contains(typeReference.Module))
            {
                Modules.Add(typeReference.Module);
                if (!_loadedAssemblies.Contains(typeReference.Module.Assembly))
                {
                    LoadAssembly(typeReference.Module.Assembly);
                }
            }

            var type = typeReference.Resolve();
            Types.Add(type);

            if (!type.Module.Assembly.IsSystemLibrary())
            {
                GetTypeReferences(type);
            }
        }

        return Task.CompletedTask;
    }


    private void LoadAssembly(AssemblyDefinition assembly)
    {
        if (_loadedAssemblies.Contains(assembly))
        {
            return;
        }

        _loadedAssemblies.Add(assembly);
        if (assembly.IsSystemLibrary())
            return;

        foreach (var module in assembly.Modules)
        {
            Modules.Add(module);
            foreach (var type in module.GetTypes())
            {
                _typesToImport.Enqueue(type);
            }
        }
    }

    private void GetTypeReferences(TypeDefinition typeDefinition)
    {
        var instructions = new List<Instruction>();
        //add method, property and field instructions
        if (typeDefinition.HasMethods)
        {
            instructions.AddRange(
                typeDefinition.Methods.SelectMany(m => m.Body?.Instructions ?? Enumerable.Empty<Instruction>()));
        }

        if (typeDefinition.HasProperties)
        {
            instructions.AddRange(typeDefinition.Properties.SelectMany(p =>
                p.GetMethod?.Body?.Instructions ?? Enumerable.Empty<Instruction>()));
            instructions.AddRange(typeDefinition.Properties.SelectMany(p =>
                p.SetMethod?.Body?.Instructions ?? Enumerable.Empty<Instruction>()));
        }

        MethodDefinition? currentMethod = null;
        foreach (var instruction in instructions)
        {
            switch (instruction.Operand)
            {
                case MethodDefinition methodDefinition:
                    currentMethod = methodDefinition;
                    break;
                case MethodReference methodReference:
                    if (methodReference.DeclaringType != typeDefinition)
                    {
                        References.Add(new Reference
                        {
                            FromType = typeDefinition,
                            ToType = methodReference.DeclaringType.Resolve(),
                            ReferenceType = methodReference,
                            FromName = currentMethod?.Name,
                            ToName = methodReference.Name
                        });
                        if (!methodReference.DeclaringType.Module.Assembly.IsSystemLibrary())
                            _typesToImport.Enqueue(methodReference.DeclaringType);
                    }

                    break;
                case PropertyDefinition propertyDefinition:
                    //GetTypeReferences(propertyDefinition.DeclaringType);
                    break;
                case PropertyReference propertyReference:
                    // GetTypeReferences(propertyReference.DeclaringType.Resolve());
                    break;
                case FieldDefinition fieldDefinition:
                    // GetTypeReferences(fieldDefinition.DeclaringType);
                    break;
                case FieldReference fieldReference:
                    break;
                    if (fieldReference.DeclaringType != typeDefinition)
                    {
                        References.Add(new Reference
                        {
                            FromType = typeDefinition,
                            ToType = fieldReference.DeclaringType.Resolve(),
                            ReferenceType = fieldReference,
                            FromName = currentMethod?.Name,
                            ToName = fieldReference.Name
                        });
                        if (!fieldReference.DeclaringType.Scope.IsSystemLibrary())
                            _typesToImport.Enqueue(fieldReference.DeclaringType);
                    }
                    break;
            }
        }
    }
}