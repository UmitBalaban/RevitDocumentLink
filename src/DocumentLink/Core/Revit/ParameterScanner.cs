using Autodesk.Revit.DB;
using DocumentLink.Core.Models;
using DocumentLink.Infrastructure;

namespace DocumentLink.Core.Revit;

/// <summary>Builds the union of instance + type parameters for a set of elements. API context.</summary>
public static class ParameterScanner
{
    public static List<ParamInfo> Scan(Document doc, IEnumerable<Element> elements)
    {
        var map = new Dictionary<string, ParamInfo>();
        var seenTypes = new HashSet<long>();

        foreach (var e in elements)
        {
            foreach (Parameter p in e.Parameters)
                Add(p, ParamScope.Instance);

            var t = ParamResolver.GetType(e);
            if (t is not null && seenTypes.Add(t.Id.Id()))
                foreach (Parameter p in t.Parameters)
                    Add(p, ParamScope.Type);
        }

        return map.Values
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Scope)
            .ToList();

        void Add(Parameter p, ParamScope scope)
        {
            if (p.Definition is null) return;
            var key = ParamResolver.KeyOf(p, scope).ToString();
            if (!map.ContainsKey(key))
                map[key] = ParamResolver.Describe(p, scope, doc);
        }
    }
}
