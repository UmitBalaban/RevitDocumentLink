using Autodesk.Revit.DB;
using ParamBridge.Core.Models;

namespace ParamBridge.Core.Revit;

/// <summary>Reads Revit values into Excel-ready values and builds SheetData. API context.</summary>
public static class ValueReader
{
    public static List<SheetData> BuildSheets(Document doc, List<Element> elements, ExportRequest req,
        IReadOnlyDictionary<string, ParamInfo> infoByKey, Action<int, int>? progress = null,
        CancellationToken ct = default)
    {
        var columns = ParamKey.Fixed.Select(FixedInfo)
            .Concat(req.Parameters.Select(k => infoByKey.TryGetValue(k.ToString(), out var i) ? i : Unknown(k)))
            .ToList();

        var groups = req.SingleSheet
            ? elements.GroupBy(_ => "Data").ToArray()
            : elements.GroupBy(e => e.Category?.Name ?? "Other").ToArray();

        var sheets = new List<SheetData>();
        int done = 0, total = elements.Count;
        foreach (var g in groups)
        {
            var sheet = new SheetData { Name = g.Key, Columns = columns };
            foreach (var e in g)
            {
                ct.ThrowIfCancellationRequested();
                var values = new object?[columns.Count];
                var kinds = new CellKind[columns.Count];
                for (int c = 0; c < columns.Count; c++)
                    (values[c], kinds[c]) = Read(e, columns[c].Key);
                sheet.Rows.Add(new RowData { UniqueId = e.UniqueId, ElementId = e.Id.Value, Values = values, Kinds = kinds });
                if (++done % 200 == 0) progress?.Invoke(done, total);
            }
            AddLists(doc, sheet);
            sheets.Add(sheet);
        }
        progress?.Invoke(total, total);
        return sheets;
    }

    public static (object? value, CellKind kind) Read(Element e, ParamKey key)
    {
        if (key.Origin == ParamOrigin.Pseudo)
        {
            return key.Id switch
            {
                "ID" => (e.Id.Value, CellKind.ReadOnly),
                "CAT" => (e.Category?.Name ?? "", CellKind.ReadOnly),
                "FAM" => (ElementSource.FamilyName(e), CellKind.ReadOnly),
                "TYPE" => (ElementSource.TypeName(e), e.GetTypeId() == ElementId.InvalidElementId ? CellKind.ReadOnly : CellKind.Editable),
                _ => (null, CellKind.Missing),
            };
        }

        var p = ParamResolver.Find(e, key);
        if (p is null) return (null, CellKind.Missing);

        var kind = p.IsReadOnly ? CellKind.ReadOnly
            : key.Scope == ParamScope.Type ? CellKind.Type
            : CellKind.Editable;
        if (e.Document.IsLinked) kind = CellKind.Linked;
        if (kind == CellKind.Editable && e.GroupId != ElementId.InvalidElementId && !p.VariesAcrossGroups)
            kind = CellKind.ReadOnly;

        return (ToExcel(p, e.Document), kind);
    }

    /// <summary>Converts a parameter value to string / double / long / null in display units.</summary>
    public static object? ToExcel(Parameter p, Document doc)
    {
        if (!p.HasValue) return null;
        var dataType = p.Definition.GetDataType();
        switch (p.StorageType)
        {
            case StorageType.String:
                return p.AsString() ?? "";
            case StorageType.Integer:
                if (dataType == SpecTypeId.Boolean.YesNo) return p.AsInteger() == 1 ? "Yes" : "No";
                return (long)p.AsInteger();
            case StorageType.Double:
                var raw = p.AsDouble();
                if (dataType is not null && !dataType.Empty() && UnitUtils.IsMeasurableSpec(dataType))
                {
                    var unit = doc.GetUnits().GetFormatOptions(dataType).GetUnitTypeId();
                    return UnitUtils.ConvertFromInternalUnits(raw, unit);
                }
                return raw;
            case StorageType.ElementId:
                var id = p.AsElementId();
                if (id == ElementId.InvalidElementId) return "";
                return doc.GetElement(id)?.Name ?? p.AsValueString() ?? "";
            default:
                return p.AsValueString();
        }
    }

    /// <summary>Dropdown lists for Yes/No, Level, Phase, Type columns.</summary>
    private static void AddLists(Document doc, SheetData sheet)
    {
        for (int c = 0; c < sheet.Columns.Count; c++)
        {
            var info = sheet.Columns[c];
            if (info.DataType.Contains("Yes/No", StringComparison.OrdinalIgnoreCase))
                sheet.Lists[c] = new() { "Yes", "No" };
            else if (info.Key.Id is "LEVEL_PARAM" or "FAMILY_LEVEL_PARAM" or "SCHEDULE_LEVEL_PARAM")
                sheet.Lists[c] = new FilteredElementCollector(doc).OfClass(typeof(Level)).Select(l => l.Name).OrderBy(n => n).ToList();
            else if (info.Key.Id is "PHASE_CREATED" or "PHASE_DEMOLISHED")
                sheet.Lists[c] = doc.Phases.Cast<Phase>().Select(p => p.Name).ToList();
            // TODO(M2): Material, key schedules, family types per family (TYPE column).
        }
    }

    private static ParamInfo FixedInfo(ParamKey k) => new()
    {
        Key = k,
        Name = k.Id switch { "ID" => "Element Id", "CAT" => "Category", "FAM" => "Family", _ => "Type" },
        IsReadOnly = k.Id != "TYPE",
        DataType = "Text",
    };

    private static ParamInfo Unknown(ParamKey k) => new() { Key = k, Name = k.Id, IsReadOnly = true };
}
