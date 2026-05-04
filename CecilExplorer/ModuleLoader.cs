using System.Collections;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CecilExplorer;

public class ModuleLoader
{
    private static readonly SemaphoreSlim AssemlbySemaphore = new(1, 1);
    private static readonly SemaphoreSlim TypeSemaphore = new(1, 1);
    
    private readonly ConcurrentBag<AssemblyDefinition> _loadedAssemblies = [];
    private readonly ConcurrentQueue<TypeReference> _typesToImport = [];
    private readonly Regex _internalNamePattern;

    public readonly Dictionary<string,TypeDefinition> Types = new ();
    public readonly ConcurrentBag<Reference> References = [];
    public readonly ConcurrentBag<ModuleDefinition> Modules = [];
    private readonly string _fileName;
    private readonly ConcurrentBag<string> _failedAssemblies = new();
    private readonly string _folder;

    public ModuleLoader(string file, string internalNamePattern)
    {
        _internalNamePattern = PatternHelper.GetPattern(internalNamePattern);
        _fileName = file;
        _folder = Path.GetDirectoryName(file) ?? string.Empty;
    }

    public async Task<Workspace> Load()
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

        var tasks = new List<Task>();
        for (var i = 0; i < 1; i++)
        {
            tasks.Add(Task.Run(StartLoading));
        }

        await Task.WhenAll(tasks);
        return new Workspace(_loadedAssemblies.ToArray(), Types.Values.ToArray(), References.ToArray());
    }

    private async Task StartLoading()
    {
        while (_typesToImport.TryDequeue(out var typeReference))
        {
            if (Types.ContainsKey(typeReference.GetElementType().FullName))
            {
                continue;
            }

            var type = await LoadTypeByReference(typeReference);
            await TypeSemaphore.WaitAsync();
            Types.TryAdd(type.FullName, type);
            TypeSemaphore.Release();

            if (IsUserModule(type))
            {
                await LoadTypesFromInheritance(type);
                await LoadTypesFromInstructions(type);
            }
        }
    }

    private async Task LoadTypesFromInheritance(TypeDefinition typeDefinition)
    {
        if (typeDefinition.BaseType != null)
        {
            var toType = await LoadTypeByReference(typeDefinition.BaseType);
            References.Add(new Reference
            {
                FromType = typeDefinition,
                ToType = toType,
                ReferenceType = typeDefinition.BaseType,
                FromName = typeDefinition.FullName,
                ToName = toType.FullName
            });
            if (IsUserModule(toType))
            {
                _typesToImport.Enqueue(toType);
            }
        }
        
        foreach (var interfaceDef in typeDefinition.Interfaces)
        {
            var toInterface = await LoadTypeByReference(interfaceDef.InterfaceType);
            References.Add(new Reference
            {
                FromType = typeDefinition,
                ToType = toInterface,
                ReferenceType = interfaceDef,
                FromName = typeDefinition.FullName,
                ToName = toInterface.FullName
            });
            if (IsUserModule(interfaceDef.InterfaceType))
                _typesToImport.Enqueue(interfaceDef.InterfaceType);
        }
    }

    private bool IsUserModule(TypeReference type)
    {
        if (type.Scope is AssemblyNameReference anr)
        {
            return _internalNamePattern.IsMatch(anr.Name) && !anr.IsSystemLibrary();
        }

        try
        {
            return type.Module != null
                ? _internalNamePattern.IsMatch(type.Module.Assembly.Name.Name)
                  && !type.Module.Assembly.IsSystemLibrary()
                : _internalNamePattern.IsMatch(type.FullName) && !type.IsSystemReference();
        }
        catch (Exception e)
        {
            return false;
        }
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

    private async Task LoadTypesFromInstructions(TypeDefinition typeDefinition)
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
                    if (true)
                    {
                        var toType = await LoadTypeByReference(methodReference.DeclaringType);
                        References.Add(new Reference
                        {
                            FromType = typeDefinition,
                            ToType = toType,
                            ReferenceType = methodReference,
                            FromName = currentMethod?.Name ?? "_",
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

    private async Task<TypeDefinition> LoadTypeByReference(TypeReference typeReference)
    {
        var anr = typeReference.Scope as AssemblyNameReference;
        if (anr != null && _failedAssemblies.Contains(anr.FullName)) return GetDefault(typeReference);

        var loadedType = Types.GetValueOrDefault(typeReference.FullName);
        if (loadedType != null) return loadedType;

        try
        {
            if (!IsUserModule(typeReference)) return GetDefault(typeReference);

            var referencedAssembly = await GetReferencedAssemmbly(typeReference);
            if (referencedAssembly == null) return GetDefault(typeReference);
            if (typeReference.Module.Assembly.Equals(referencedAssembly)) return typeReference.Resolve();

            var type = referencedAssembly.Modules
                .Select(m => m.GetTypes().First(t => t.FullName.Equals(typeReference.FullName))).First();

            return type;
        }
        catch (Exception e)
        {
            _failedAssemblies.Add(anr != null
                ? anr.FullName
                : typeReference.Module.Assembly.FullName);
        }

        return GetDefault(typeReference);
        TypeDefinition GetDefault(TypeReference r) => new(r.Namespace, r.Name, TypeAttributes.Class);
    }

    private async Task<AssemblyDefinition?> GetReferencedAssemmbly(TypeReference typeReference)
    {
        await AssemlbySemaphore.WaitAsync();
        var referencedAssembly = _loadedAssemblies.FirstOrDefault(a => a.FullName.Equals(typeReference.AssemblyName()));
        if (referencedAssembly != null)
        {
            AssemlbySemaphore.Release();
            return referencedAssembly;
        }

        try
        {
            referencedAssembly =
                AssemblyDefinition.ReadAssembly(Path.Combine(_folder, $"{typeReference.Scope.Name}.dll"));
            LoadAssembly(referencedAssembly);
        }
        catch (Exception e)
        {
            return null;
        }
        finally
        {
            AssemlbySemaphore.Release();
        }

        return referencedAssembly;
    }
}