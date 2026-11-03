using Autodesk.Revit.DB;
using DocumentLink.Core.Models;

namespace DocumentLink.Core.Revit;

/// <summary>Maps between Revit Parameter objects and ParamKey. Must run in API context.</summary>
public static class ParamResolver
{
    public static ParamKey KeyOf(Parameter p, ParamScope scope)
    {
        if (p.Definition is InternalDefinition idef && idef.BuiltInParameter != BuiltInParameter.INVALID)
            return new ParamKey(scope, ParamOrigin.BuiltIn, idef.BuiltInParameter.ToString());
        if (p.IsShared)
            return new ParamKey(scope, ParamOrigin.Shared, p.GUID.ToString());
        return new ParamKey(scope, ParamOrigin.ProjectOrFamily, p.Definition.Name);
    }

    /// <summary>Returns the parameter on the element (instance key) or on its type (type key).</summary>
    public static Parameter? Find(Element element, ParamKey key)
    {
        var host = key.Scope == ParamScope.Type ? GetType(element) : element;
        if (host is null) return null;
        return key.Origin switch
        {
            ParamOrigin.BuiltIn when Enum.TryParse<BuiltInParameter>(key.Id, out var bip) => host.get_Parameter(bip),
            ParamOrigin.Shared when Guid.TryParse(key.Id, out var g) => host.get_Parameter(g),
            ParamOrigin.ProjectOrFamily => host.LookupParameter(key.Id),
            _ => null,
        };
    }

    public static Element? GetType(Element e)
    {
        var tid = e.GetTypeId();
        return tid == ElementId.InvalidElementId ? null : e.Document.GetElement(tid);
    }

    public static ParamInfo Describe(Parameter p, ParamScope scope, Document doc)
    {
        var dataType = p.Definition.GetDataType();
        string unit = "";
        if (dataType is not null && !dataType.Empty() && UnitUtils.IsMeasurableSpec(dataType))
        {
            try
            {
                var fo = doc.GetUnits().GetFormatOptions(dataType);
                var symbol = fo.GetSymbolTypeId();
                unit = symbol is not null && !symbol.Empty()
                    ? LabelUtils.GetLabelForSymbol(symbol)
                    : LabelUtils.GetLabelForUnit(fo.GetUnitTypeId());
            }
            catch { /* some specs have no format options */ }
        }

        string group = "";
        try { group = LabelUtils.GetLabelForGroup(p.Definition.GetGroupTypeId()); } catch { }

        string typeLabel = p.StorageType.ToString();
        try
        {
            if (dataType is not null && !dataType.Empty())
                typeLabel = LabelUtils.GetLabelForSpec(dataType);
        }
        catch { }

        return new ParamInfo
        {
            Key = KeyOf(p, scope),
            Name = p.Definition.Name,
            DataType = typeLabel,
            Group = group,
            IsReadOnly = p.IsReadOnly,
            Unit = unit,
        };
    }
}
