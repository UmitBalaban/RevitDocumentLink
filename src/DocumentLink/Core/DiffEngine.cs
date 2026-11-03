using System.Globalization;
using Autodesk.Revit.DB;
using DocumentLink.Core.Models;
using DocumentLink.Infrastructure;
using DocumentLink.Core.Revit;

namespace DocumentLink.Core;

/// <summary>Compares sheets read from Excel with the current model and produces CellChanges. API context.</summary>
public static class DiffEngine
{
    public static List<CellChange> Diff(Document doc, IEnumerable<SheetData> sheets, CultureInfo culture)
    {
        var changes = new List<CellChange>();

        foreach (var sheet in sheets)
        {
            foreach (var row in sheet.Rows)
            {
                var e = string.IsNullOrEmpty(row.UniqueId) ? null : doc.GetElement(row.UniqueId);
                if (e is null)
                {
                    changes.Add(Make(sheet, row, null, sheet.Columns[0], "", row.ElementId.ToString(),
                        ChangeStatus.Error, "Element not found in document."));
                    continue;
                }

                for (int c = 0; c < sheet.Columns.Count; c++)
                {
                    var col = sheet.Columns[c];
                    if (col.Key.Origin == ParamOrigin.Pseudo && col.Key.Id != "TYPE") continue;

                    var newText = Format(row.Values[c], culture);
                    var (current, kind) = ValueReader.Read(e, col.Key);
                    var oldText = Format(current, culture);
                    if (Same(oldText, newText, current, row.Values[c])) continue;

                    var (status, reason) = kind switch
                    {
                        CellKind.ReadOnly => (ChangeStatus.Error, "Read-only parameter."),
                        CellKind.Missing => (ChangeStatus.Error, "Parameter does not exist for this element."),
                        CellKind.Linked => (ChangeStatus.Error, "Elements in linked models cannot be updated."),
                        _ => (ChangeStatus.WillApply, ""),
                    };
                    changes.Add(Make(sheet, row, e, col, oldText, newText, status, reason, row.Values[c]));
                }
            }
        }

        FlagTypeConflicts(changes);
        return changes;
    }

    /// <summary>Type parameters: all instances of a type must carry the same new value.</summary>
    private static void FlagTypeConflicts(List<CellChange> changes)
    {
        var typeChanges = changes.Where(c => c.Key.Scope == ParamScope.Type && c.Status == ChangeStatus.WillApply)
            .GroupBy(c => (c.TargetTypeId, Key: c.Key.ToString()));
        foreach (var g in typeChanges)
        {
            var distinct = g.Select(c => c.NewValue).Distinct().Count();
            if (distinct > 1)
            {
                foreach (var c in g) { c.Status = ChangeStatus.Error; c.Reason = $"Instances of type '{c.TypeName}' have different values for this type parameter."; }
            }
            else
            {
                // keep a single change per type; the others are duplicates
                foreach (var c in g.Skip(1)) { c.Status = ChangeStatus.Warning; c.Reason = "Same type change already listed."; c.IsSelected = false; }
            }
        }
        // NOTE: rows of the same type that were NOT edited are not in the list; DiffEngine v2 should
        // detect "some instances changed, others not" by comparing against all rows of that type in the sheet.
    }

    private static CellChange Make(SheetData sheet, RowData row, Element? e, ParamInfo col, string oldV, string newV,
        ChangeStatus status, string reason, object? raw = null) => new()
    {
        Sheet = sheet.Name,
        UniqueId = row.UniqueId,
        ElementId = row.ElementId,
        Category = e?.Category?.Name ?? "",
        TypeName = e is null ? "" : ElementSource.TypeName(e),
        TargetTypeId = e?.GetTypeId().Id() ?? -1,
        Key = col.Key,
        ParameterName = col.Name,
        OldValue = oldV,
        NewValue = newV,
        RawNewValue = raw,
        Status = status,
        Reason = reason,
        IsSelected = status != ChangeStatus.Error,
    };

    public static string Format(object? v, CultureInfo culture) => v switch
    {
        null => "",
        double d => d.ToString("0.######", culture),
        IFormattable f => f.ToString(null, culture),
        _ => v.ToString() ?? "",
    };

    private static bool Same(string oldText, string newText, object? oldRaw, object? newRaw)
    {
        if (oldRaw is double a && newRaw is double b) return Math.Abs(a - b) < 1e-6 * Math.Max(1, Math.Abs(a));
        return string.Equals(oldText, newText, StringComparison.Ordinal);
    }
}
