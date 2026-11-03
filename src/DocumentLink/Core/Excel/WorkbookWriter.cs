using System.Globalization;
using ClosedXML.Excel;
using DocumentLink.Core.Models;
using static DocumentLink.Core.Excel.WorkbookFormat;

namespace DocumentLink.Core.Excel;

public sealed class WorkbookOptions
{
    public bool IncludeInstructions { get; init; } = true;
    public bool ProtectReadOnly { get; init; } = true;
    public bool ReplaceExistingSheets { get; init; } = true;
    public string DocumentTitle { get; init; } = "";
    public string DocumentGuid { get; init; } = "";
    public string RevitVersion { get; init; } = "";
}

/// <summary>Writes SheetData to .xlsx following docs/04_EXCEL_FORMAT.md. No Revit dependency.</summary>
public static class WorkbookWriter
{
    public static void Write(string path, IReadOnlyList<SheetData> sheets, WorkbookOptions options)
    {
        using var wb = File.Exists(path) ? new XLWorkbook(path) : new XLWorkbook();
        var used = new HashSet<string>(wb.Worksheets.Select(w => w.Name));

        var meta = GetOrAdd(wb, MetaSheet);
        meta.Clear();
        var metaRow = 1;
        void M(string k, string v) { meta.Cell(metaRow, 1).Value = k; meta.Cell(metaRow, 2).Value = v; metaRow++; }
        M("format", FormatVersion);
        M("revitVersion", options.RevitVersion);
        M("documentTitle", options.DocumentTitle);
        M("documentGuid", options.DocumentGuid);
        M("exportedAtUtc", DateTime.UtcNow.ToString("o"));
        M("culture", CultureInfo.InvariantCulture.Name);

        var lists = GetOrAdd(wb, ListsSheet);
        lists.Clear();
        var listCol = 1;

        if (options.IncludeInstructions) WriteInstructions(wb, options);

        foreach (var sheet in sheets)
        {
            if (options.ReplaceExistingSheets && wb.Worksheets.TryGetWorksheet(sheet.Name, out var old))
            {
                old.Delete();
                used.Remove(sheet.Name);
            }
            var name = SafeSheetName(sheet.Name, used);
            var ws = wb.Worksheets.Add(name);
            WriteSheet(ws, sheet, lists, ref listCol);
            M($"sheet:{name}:keys", KeysHash(sheet.Columns.Select(c => c.Key.ToString())));
        }

        meta.Visibility = XLWorksheetVisibility.VeryHidden;
        lists.Visibility = XLWorksheetVisibility.VeryHidden;
        wb.SaveAs(path);
    }

    private static void WriteSheet(IXLWorksheet ws, SheetData sheet, IXLWorksheet lists, ref int listCol)
    {
        int nCols = sheet.Columns.Count;
        int lastCol = FirstParamCol + nCols - 1;

        ws.Cell(KeyRow, UidCol).Value = Marker;
        ws.Cell(HeaderRow, UidCol).Value = "UniqueId";
        for (int c = 0; c < nCols; c++)
        {
            var info = sheet.Columns[c];
            ws.Cell(KeyRow, FirstParamCol + c).Value = info.Key.ToString();
            var h = ws.Cell(HeaderRow, FirstParamCol + c);
            h.Value = info.Header;
            var note = $"{KindLabel(info)}\n{info.DataType}\n{info.Group}";
            if (info.Scope == ParamScope.Type) note += "\nAll instances of a type must have the same value.";
            h.CreateComment().AddText(note);
        }

        var header = ws.Range(HeaderRow, UidCol, HeaderRow, lastCol);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = Header;

        int r = FirstDataRow;
        foreach (var row in sheet.Rows)
        {
            ws.Cell(r, UidCol).Value = row.UniqueId;
            for (int c = 0; c < nCols; c++)
            {
                var cell = ws.Cell(r, FirstParamCol + c);
                SetValue(cell, row.Values[c]);
                Style(cell, row.Kinds[c]);
            }
            r++;
        }
        int lastRow = Math.Max(FirstDataRow, r - 1);

        // text columns keep leading zeros etc.
        for (int c = 0; c < nCols; c++)
            if (sheet.Columns[c].DataType is "Text" or "")
                ws.Range(FirstDataRow, FirstParamCol + c, lastRow, FirstParamCol + c).Style.NumberFormat.Format = "@";

        foreach (var kv in sheet.Lists)
        {
            int colIndex = kv.Key; var values = kv.Value;
            for (int i = 0; i < values.Count; i++) lists.Cell(i + 1, listCol).Value = values[i];
            var src = lists.Range(1, listCol, Math.Max(1, values.Count), listCol);
            var dv = ws.Range(FirstDataRow, FirstParamCol + colIndex, lastRow, FirstParamCol + colIndex).CreateDataValidation();
            dv.List(src, true);
            dv.ErrorTitle = "Invalid value";
            dv.ErrorMessage = "Please choose a value from the list.";
            listCol++;
        }

        ws.Row(KeyRow).Hide();
        ws.Column(UidCol).Hide();
        ws.SheetView.Freeze(HeaderRow, UidCol + 1);
        ws.Range(HeaderRow, FirstParamCol, lastRow, lastCol).SetAutoFilter();
        ws.Columns(FirstParamCol, lastCol).AdjustToContents(HeaderRow, Math.Min(lastRow, 500), 8, 60);

        // structural rows/cols stay locked; data cells decided per kind in Style()
        var prot = ws.Protect();
        prot.AllowElement(XLSheetProtectionElements.SelectEverything);
        prot.AllowElement(XLSheetProtectionElements.FormatColumns);
        prot.AllowElement(XLSheetProtectionElements.Sort);
        prot.AllowElement(XLSheetProtectionElements.AutoFilter);
    }

    private static void SetValue(IXLCell cell, object? v)
    {
        switch (v)
        {
            case null: cell.Value = Blank.Value; break;
            case double d: cell.Value = d; break;
            case long l: cell.Value = (double)l; cell.Style.NumberFormat.Format = "0"; break;
            case int i: cell.Value = (double)i; cell.Style.NumberFormat.Format = "0"; break;
            default: cell.Value = v.ToString(); break;
        }
    }

    private static void Style(IXLCell cell, CellKind kind)
    {
        switch (kind)
        {
            case CellKind.Editable:
                cell.Style.Protection.Locked = false;
                break;
            case CellKind.Type:
                cell.Style.Protection.Locked = false;
                cell.Style.Fill.BackgroundColor = TypeFill;
                break;
            case CellKind.ReadOnly:
            case CellKind.Linked:
                cell.Style.Fill.BackgroundColor = ReadOnlyFill;
                cell.Style.Font.FontColor = ReadOnlyFont;
                break;
            case CellKind.Missing:
                cell.Style.Fill.BackgroundColor = MissingFill;
                break;
        }
    }

    private static void WriteInstructions(XLWorkbook wb, WorkbookOptions o)
    {
        var ws = GetOrAdd(wb, InstructionsSheet);
        ws.Clear();
        ws.Position = 1;
        string[] lines =
        {
            "DocumentLink – Revit data workbook",
            $"Model: {o.DocumentTitle}    Exported: {DateTime.Now:yyyy-MM-dd HH:mm}",
            "",
            "Colour legend",
            "  No fill – editable instance value",
            "  Yellow – type value (applies to every element of that type; keep it identical for all of them)",
            "  Red – read-only value, will not be imported",
            "  Dark grey – the parameter does not exist for this element",
            "",
            "Rules",
            "  • Do not edit the hidden first row or hidden first column – they link rows to Revit elements.",
            "  • Do not insert columns before column B or rename sheets.",
            "  • When using Find & Replace, select the cells first so hidden cells are not touched.",
            "  • You may delete or sort rows.",
            "",
            "Import back: Revit → DocumentLink → Excel Sync → Import…",
        };
        for (int i = 0; i < lines.Length; i++) ws.Cell(i + 1, 1).Value = lines[i];
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(6, 1).Style.Fill.BackgroundColor = TypeFill;
        ws.Cell(7, 1).Style.Fill.BackgroundColor = ReadOnlyFill;
        ws.Cell(8, 1).Style.Fill.BackgroundColor = MissingFill;
        ws.Column(1).Width = 100;
    }

    private static IXLWorksheet GetOrAdd(XLWorkbook wb, string name) =>
        wb.Worksheets.TryGetWorksheet(name, out var ws) ? ws : wb.Worksheets.Add(name);
}
