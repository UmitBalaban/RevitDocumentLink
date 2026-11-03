using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DocumentLink.Core.Models;
using DocumentLink.Infrastructure;

namespace DocumentLink.Core.Revit;

public sealed record CategoryItem(long Id, string Name, string BuiltInName, CategoryType Type, int Count);

/// <summary>Collects elements for each source tab. Must run in API context.</summary>
public static class ElementSource
{
    public static List<CategoryItem> ListCategories(UIApplication app, CategoryType type, ElementScope scope)
    {
        var uidoc = app.ActiveUIDocument;
        var doc = uidoc.Document;
        var counts = new Dictionary<long, int>();
        foreach (var e in Scoped(uidoc, scope).WhereElementIsNotElementType())
        {
            var c = e.Category;
            if (c is null || c.CategoryType != type) continue;
            counts[c.Id.Id()] = counts.TryGetValue(c.Id.Id(), out var n) ? n + 1 : 1;
        }

        var result = new List<CategoryItem>();
        foreach (Category c in doc.Settings.Categories)
        {
            if (c.CategoryType != type || !counts.TryGetValue(c.Id.Id(), out var n)) continue;
            var bic = c.BuiltInCategory;
            result.Add(new CategoryItem(c.Id.Id(), c.Name, bic.ToString(), type, n));
        }
        return result.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static List<Element> Collect(UIApplication app, ExportRequest req)
    {
        var uidoc = app.ActiveUIDocument;
        var doc = uidoc.Document;
        IEnumerable<Element> elements = req.Tab switch
        {
            SourceTab.Elements => req.ElementIds.Select(id => doc.GetElement(id.ToElementId())).Where(e => e is not null)!,
            SourceTab.Schedules => req.ScheduleIds.SelectMany(id =>
                new FilteredElementCollector(doc, id.ToElementId()).WhereElementIsNotElementType().ToElements()),
            _ => Scoped(uidoc, req.Scope).WhereElementIsNotElementType()
                    .Where(e => e.Category is not null && req.CategoryIds.Contains(e.Category.Id.Id())),
        };

        var list = elements.Distinct(new IdComparer()).ToList();
        return req.SortByType
            ? list.OrderBy(e => e.Category?.Name).ThenBy(e => e.GetTypeId().Id()).ThenBy(e => e.Id.Id()).ToList()
            : list.OrderBy(e => e.Category?.Name).ThenBy(e => FamilyName(e)).ThenBy(e => e.Id.Id()).ToList();
    }

    public static List<long> CurrentSelection(UIApplication app) =>
        app.ActiveUIDocument.Selection.GetElementIds().Select(i => i.Id()).ToList();

    public static string FamilyName(Element e) =>
        e is FamilyInstance fi ? fi.Symbol.FamilyName
        : (ParamResolver.GetType(e) as ElementType)?.FamilyName ?? "";

    public static string TypeName(Element e) => ParamResolver.GetType(e)?.Name ?? "";

    private static FilteredElementCollector Scoped(UIDocument uidoc, ElementScope scope)
    {
        var doc = uidoc.Document;
        return scope switch
        {
            ElementScope.ActiveView => new FilteredElementCollector(doc, doc.ActiveView.Id),
            ElementScope.Selection when uidoc.Selection.GetElementIds().Count > 0 =>
                new FilteredElementCollector(doc, uidoc.Selection.GetElementIds()),
            _ => new FilteredElementCollector(doc),
        };
    }

    private sealed class IdComparer : IEqualityComparer<Element>
    {
        public bool Equals(Element? x, Element? y) => x?.Id == y?.Id;
        public int GetHashCode(Element obj) => obj.Id.GetHashCode();
    }
}
