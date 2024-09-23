using Mono.Cecil;

namespace CecilExplorer;

public class Reference
{
    public TypeDefinition FromType { get; set; }
    public TypeDefinition ToType { get; set; }
    public object ReferenceType { get; set; }
    public string ReferenceName { get; set; }

    public string ToString()
    {
        return $"{FromType.FullName} -> {ToType.FullName} ({ReferenceName})";
    }
}