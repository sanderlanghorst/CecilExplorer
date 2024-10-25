using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CecilExplorer;

public class ModuleLoader
{
    private readonly List<AssemblyDefinition> _loadedAssemblies = [];
    private ConcurrentQueue<ModuleReference> _assembliesToImport = [];
    private readonly ConcurrentQueue<TypeReference> _typesToImport = [];
    private readonly Regex _internalNamePattern;

    public List<TypeDefinition> Types = [];
    public List<Reference> References = [];
    public List<ModuleDefinition> Modules = [];
    private readonly string _fileName;

    public ModuleLoader(string file, string internalNamePattern)
    {
        _internalNamePattern = GetPattern(internalNamePattern);
        _fileName = file;
    }

    private Regex GetPattern(string internalNamePattern)
    {
        var pattern = string.Join("|", internalNamePattern.Split(',')
            .Select(s => s
                .Replace(".", "\\.")
                .Replace("*", ".{0,100}")
                .Replace("?", string.Empty)
                .Replace("+", string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Trim())
        );
        
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    public Task Load()
    {
        var assembly = AssemblyDefinition.ReadAssembly(_fileName);
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

            if (IsUserModule(type))
            {
                GetTypeReferences(type);
            }
        }

        return Task.CompletedTask;
    }
    
    private bool IsUserModule(TypeReference type)
    {
        return _internalNamePattern.IsMatch(type.Module.Assembly.FullName)
               && !type.Module.Assembly.IsSystemLibrary();
    }
    private bool IsUserModule(AssemblyDefinition assembly)
    {
        return _internalNamePattern.IsMatch(assembly.FullName)
               && !assembly.IsSystemLibrary();
    }


    private void LoadAssembly(AssemblyDefinition assembly)
    {
        if (_loadedAssemblies.Contains(assembly))
        {
            return;
        }

        _loadedAssemblies.Add(assembly);
        if (!IsUserModule(assembly))
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
                        if (IsUserModule(methodReference.DeclaringType))
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