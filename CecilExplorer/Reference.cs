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

    public bool Intersects(Reference otherReference)
    {
        return (otherReference.FromType == FromType && otherReference.FromName == FromName)
            || (otherReference.ToType == FromType && otherReference.ToName == FromName)
            || (otherReference.FromType == ToType && otherReference.FromName == ToName)
            || (otherReference.ToType == ToType && otherReference.ToName == ToName);
    }
}