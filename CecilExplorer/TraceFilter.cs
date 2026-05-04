namespace CecilExplorer;

public class TraceFilter : IFilter
{
    private readonly string _fromType;
    private readonly string _toType;

    public TraceFilter(string fromType, string toType)
    {
        _fromType = fromType;
        _toType = toType;
    }
    
    public Workspace Filter(Workspace workspace)
    {
        var fromType = workspace.Types.FirstOrDefault(t => t.FullName == _fromType);
        var toType = workspace.Types.FirstOrDefault(t => t.FullName == _toType);
        if (fromType == null || toType == null)
        {
            return new Workspace([],[],[]);
        }
        var fromReferences = workspace.References.Where(r => r.FromType == fromType).Select(r => new R(null, r)).ToArray();
        var toReferences = workspace.References.Where(r => r.ToType == toType).Select(r => new R(null, r)).ToArray();
        if (fromReferences.Length == 0 || toReferences.Length == 0)
        {
            return new Workspace([],[],[]);
        }

        int maxDepth = 10;
        while (fromReferences.Length > 0 && toReferences.Length > 0 && maxDepth-- > 0)
        {
            var common = fromReferences.Join(toReferences, f => f.ToReference, t => t.ToReference, (f,t) => (f,t), new ReferenceIntersection()).ToArray();
            if (common.Length > 0)
            {
                return GetWorkspace(workspace, common);
            }
            
            var newRs = new List<R>();
            foreach (var fromReference in fromReferences)
            {
                var toRs = workspace.References.Where(r =>
                        r.FromType == fromReference.ToReference.ToType &&
                        Equals(r.FromName, fromReference.ToReference.ToName))
                    .Select(r => new R(fromReference, r)).ToArray();
                newRs.AddRange(toRs);
            }

            fromReferences = newRs.ToArray();
            newRs = new List<R>();
            foreach (var toReference in toReferences)
            {
                var toRs = workspace.References.Where(r =>
                        r.ToType == toReference.ToReference.FromType &&
                        Equals(r.ToName, toReference.ToReference.FromName))
                    .Select(r => new R(toReference, r)).ToArray();
                newRs.AddRange(toRs);
            }

            toReferences = newRs.ToArray();
        }
        
        
        return new Workspace([],[],[]);
    }

    private Workspace GetWorkspace(Workspace workspace, (R fromReferences, R toReferences)[] common)
    {
        var references = new List<Reference>();
        foreach (var reference in common)
        {
            var r = reference.fromReferences;
            do
            {
                references.Add(r.ToReference);
                r = r.BaseReference;
            } while (r != null);
        }

        foreach (var reference in common)
        {
            var r = reference.toReferences;
            do
            {
                references.Add(r.ToReference);
                r = r.BaseReference;
            } while (r != null);
        }

        var types = references.Select(r => r.FromType).Union(references.Select(r => r.ToType)).ToArray();
        var assemblies = types.Select(t => t.Module?.Assembly).Where(a => a != null).Distinct().ToArray();
        return new Workspace(assemblies, types, references.ToArray());
    }
    
    private record R(R? BaseReference, Reference ToReference);
    
    private class ReferenceIntersection : IEqualityComparer<Reference>
    {
        public bool Equals(Reference? x, Reference? y)
        {
            if (x is null || y is null) return false;
            
            return x.Intersects(y);
        }

        public int GetHashCode(Reference obj)
        {
            return 1;
        }
    }
}