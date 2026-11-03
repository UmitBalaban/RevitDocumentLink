using System.Globalization;
using Autodesk.Revit.DB;
using ParamBridge.Core.Models;

namespace ParamBridge.Core.Revit;

public sealed record ApplyResult(int Applied, int Failed, List<string> Messages);

/// <summary>Writes selected changes in one undoable TransactionGroup. API context.</summary>
public static class ChangeApplier
{
    public static ApplyResult Apply(Document doc, IEnumerable<CellChange> changes, CultureInfo culture)
    {
        var selected = changes.Where(c => c.IsSelected && c.Status != ChangeStatus.Error).ToList();
        int ok = 0, fail = 0;
        var messages = new List<string>();

        using var group = new TransactionGroup(doc, "ParamBridge: import from Excel");
        group.Start();

        foreach (var sheet in selected.GroupBy(c => c.Sheet))
        {
            using var t = new Transaction(doc, $"ParamBridge: {sheet.Key}");
            var failures = new WarningSwallower();
            var opts = t.GetFailureHandlingOptions();
            opts.SetFailuresPreprocessor(failures);
            t.SetFailureHandlingOptions(opts);
            t.Start();

            foreach (var c in sheet)
            {
                try
                {
                    var e = doc.GetElement(c.UniqueId) ?? throw new InvalidOperationException("Element not found.");
                    ValueWriter.Write(e, c.Key, c.RawNewValue, culture);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    c.Status = ChangeStatus.Error;
                    c.Reason = ex.Message;
                    messages.Add($"[{c.Sheet}] {c.ElementId} · {c.ParameterName}: {ex.Message}");
                }
            }

            if (t.Commit() != TransactionStatus.Committed)
                messages.Add($"[{sheet.Key}] transaction was rolled back by Revit.");
            messages.AddRange(failures.Warnings.Select(w => $"[{sheet.Key}] warning: {w}"));
        }

        group.Assimilate();
        return new ApplyResult(ok, fail, messages);
    }

    private sealed class WarningSwallower : IFailuresPreprocessor
    {
        public List<string> Warnings { get; } = new();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            foreach (var f in accessor.GetFailureMessages())
            {
                if (f.GetSeverity() == FailureSeverity.Warning)
                {
                    Warnings.Add(f.GetDescriptionText());
                    accessor.DeleteWarning(f);
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
