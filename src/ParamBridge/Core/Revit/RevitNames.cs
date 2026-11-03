using Autodesk.Revit.DB;

namespace ParamBridge.Core.Revit;

/// <summary>
/// Human-readable names for referenced elements (docs/08_VALUE_RULES.md) and the reverse lookup.
/// Rule: show what the Properties palette shows — never an id.
/// </summary>
public static class RevitNames
{
    public const string ByCategory = "<By Category>";

    public static string Of(Document doc, ElementId id, Parameter? p = null)
    {
        if (id == ElementId.InvalidElementId)
            return p is not null && IsMaterial(p) ? ByCategory : "";

        if (id.Value < 0)
        {
            var cat = Category.GetCategory(doc, id);
            if (cat is not null) return cat.Name;
        }

        var e = doc.GetElement(id);
        return e switch
        {
            null => p?.AsValueString() ?? "",
            SpatialElement sp => $"{sp.Number} - {sp.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString()}",
            ViewSheet vs => $"{vs.SheetNumber} - {vs.Name}",
            FamilySymbol fs => $"{fs.FamilyName}: {fs.Name}",
            ElementType et when !string.IsNullOrEmpty(et.FamilyName) && IsFamilyTypeParam(p) => $"{et.FamilyName}: {et.Name}",
            _ => e.Name,
        };
    }

    public static string Workset(Document doc, int worksetId)
    {
        if (!doc.IsWorkshared) return "";
        // WorksetId is not an ElementId; Revit shows the workset name.
        return doc.GetWorksetTable().GetWorkset(new WorksetId(worksetId))?.Name ?? "";
    }

    public static int ResolveWorkset(Document doc, string name)
    {
        if (!doc.IsWorkshared) throw new InvalidOperationException("The model is not workshared.");
        var ws = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
            .FirstOrDefault(w => w.Name == name)
            ?? throw new InvalidOperationException($"Workset '{name}' does not exist. Choose one from the dropdown list.");
        return ws.Id.IntegerValue;
    }

    /// <summary>Name → ElementId for an ElementId parameter, searching elements of the right kind.</summary>
    public static ElementId Resolve(Document doc, Parameter p, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ElementId.InvalidElementId;
        if (IsMaterial(p) && name == ByCategory) return ElementId.InvalidElementId;

        var candidates = Candidates(doc, p).Where(e => Of(doc, e.Id, p) == name || e.Name == name).ToList();
        return candidates.Count switch
        {
            1 => candidates[0].Id,
            0 => throw new InvalidOperationException($"'{name}' is not a valid value. Choose one from the dropdown list."),
            _ => throw new InvalidOperationException($"'{name}' is ambiguous ({candidates.Count} matches)."),
        };
    }

    /// <summary>Valid elements for an ElementId parameter (used for dropdowns and for Resolve).</summary>
    public static IEnumerable<Element> Candidates(Document doc, Parameter p)
    {
        var bip = (p.Definition as InternalDefinition)?.BuiltInParameter ?? BuiltInParameter.INVALID;
        if (bip is BuiltInParameter.PHASE_CREATED or BuiltInParameter.PHASE_DEMOLISHED)
            return doc.Phases.Cast<Element>();
        if (IsMaterial(p))
            return new FilteredElementCollector(doc).OfClass(typeof(Material));
        if (IsLevelParam(bip))
            return new FilteredElementCollector(doc).OfClass(typeof(Level));

        var current = doc.GetElement(p.AsElementId());
        if (current is ElementType et && et.Category is not null)
            return new FilteredElementCollector(doc).OfCategoryId(et.Category.Id).WhereElementIsElementType();
        if (current is not null)
            return new FilteredElementCollector(doc).OfClass(current.GetType());
        return new FilteredElementCollector(doc).OfClass(typeof(Level)); // TODO(M3): unknown target class with empty value → mark read-only
    }

    public static bool IsLevelParam(BuiltInParameter bip) => bip is
        BuiltInParameter.LEVEL_PARAM or BuiltInParameter.FAMILY_LEVEL_PARAM or BuiltInParameter.SCHEDULE_LEVEL_PARAM or
        BuiltInParameter.WALL_BASE_CONSTRAINT or BuiltInParameter.WALL_HEIGHT_TYPE or BuiltInParameter.FAMILY_BASE_LEVEL_PARAM or
        BuiltInParameter.FAMILY_TOP_LEVEL_PARAM or BuiltInParameter.RBS_START_LEVEL_PARAM or BuiltInParameter.ROOM_UPPER_LEVEL or
        BuiltInParameter.STAIRS_BASE_LEVEL_PARAM or BuiltInParameter.STAIRS_TOP_LEVEL_PARAM or BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM;

    public static bool IsMaterial(Parameter p)
    {
        var dt = p.Definition.GetDataType();
        return dt == SpecTypeId.Reference.Material;
    }

    private static bool IsFamilyTypeParam(Parameter? p)
    {
        if (p is null) return false;
        var dt = p.Definition.GetDataType();
        return dt is not null && Category.IsBuiltInCategory(dt) || // "Family Type" params carry a category spec
               (p.Definition as InternalDefinition)?.BuiltInParameter is BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM;
    }
}
