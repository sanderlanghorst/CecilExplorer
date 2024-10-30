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
    private ConcurrentBag<string> _failedAssemblies = new();
    private readonly string _folder;
    public ModuleLoader(string file, string internalNamePattern)
    {
        _internalNamePattern = GetPattern(internalNamePattern);
        _fileName = file;
        _folder = Path.GetDirectoryName(file) ?? string.Empty;
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
            if (Types.Any(t => t.FullName.Equals(typeReference.GetElementType().FullName)))
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

            var type = GetTypeByReference(typeReference);
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
        if (type.Scope is AssemblyNameReference anr)
        {
            return _internalNamePattern.IsMatch(anr.Name) && !anr.IsSystemLibrary(); 
        }

        try
        {
            return type.Module != null ? _internalNamePattern.IsMatch(type.Module.Assembly.Name.Name)
                   && !type.Module.Assembly.IsSystemLibrary()
                    : _internalNamePattern.IsMatch(type.FullName);    
        }catch(Exception e)
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
                        var toType = GetTypeByReference(methodReference.DeclaringType);
                        References.Add(new Reference
                        {
                            FromType = typeDefinition,
                            ToType = toType,
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
    
    private TypeDefinition GetTypeByReference(TypeReference typeReference)
    {
        var anr = typeReference.Scope as AssemblyNameReference;
        if (anr != null && _failedAssemblies.Contains(anr.FullName)) return GetDefault(typeReference);
        
        var loadedType = Types.FirstOrDefault(t => t.FullName.Equals(typeReference.FullName));
        if (loadedType != null) return loadedType;
        
        try
        {
            if (!IsUserModule(typeReference)) return GetDefault(typeReference);

            var referencedAssembly = GetReferencedAssemmbly(typeReference);
            if (referencedAssembly == null) return GetDefault(typeReference);
            if (typeReference.Module.Assembly.Equals(referencedAssembly)) return typeReference.Resolve();
            
            var type = referencedAssembly.Modules.Select(m => m.GetTypes().First(t => t.FullName.Equals(typeReference.FullName))).First();
            if (!Types.Contains(type)) Types.Add(type);
            
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

    private AssemblyDefinition? GetReferencedAssemmbly(TypeReference typeReference)
    {
        var referencedAssembly = _loadedAssemblies.FirstOrDefault(a => a.FullName.Equals(typeReference.AssemblyName()));
        if (referencedAssembly == null)
        {
            try
            {
                referencedAssembly = AssemblyDefinition.ReadAssembly(Path.Combine(_folder,$"{typeReference.Scope.Name}.dll"));
                _loadedAssemblies.Add(referencedAssembly);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        return referencedAssembly;
    }
}