using Mono.Cecil;

namespace CecilExplorer;

public class Reference
{
    public TypeDefinition FromType { get; set; }
    public TypeDefinition ToType { get; set; }
    public object ReferenceType { get; set; }
    public string FromName { get; set; }
    public string ToName { get; set; }

    public string ToString()
    {
        return $"{FromType.FullName} ({FromName}) -> {ToType.FullName} ({ToName})";
    }
}