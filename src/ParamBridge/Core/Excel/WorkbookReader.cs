using ClosedXML.Excel;
using ParamBridge.Core.Models;
using static ParamBridge.Core.Excel.WorkbookFormat;

namespace ParamBridge.Core.Excel;

public sealed class ReadResult
{
    public List<SheetData> Sheets { get; } = new();
    public List<string> Problems { get; } = new();
    public Dictionary<string, string> Meta { get; } = new();
}

/// <summary>Reads a ParamBridge workbook back into SheetData with structure validation. No Revit dependency.</summary>
public static class WorkbookReader
{
    public static ReadResult Read(string path)
    {
        var result = new ReadResult();
        using var wb = new XLWorkbook(path);

        if (wb.Worksheets.TryGetWorksheet(MetaSheet, out var meta))
            foreach (var row in meta.RowsUsed())
                result.Meta[row.Cell(1).GetString()] = row.Cell(2).GetString();

        if (!result.Meta.TryGetValue("format", out var fmt) || fmt != FormatVersion)
        {
            result.Problems.Add("This workbook was not created by ParamBridge (or was created by an incompatible version).");
            return result;
        }

        foreach (var ws in wb.Worksheets)
        {
            if (ws.Name is MetaSheet or ListsSheet or InstructionsSheet) continue;
            if (ws.Cell(KeyRow, UidCol).GetString() != Marker) continue; // user-added sheet, ignore

            var lastCol = ws.Row(KeyRow).LastCellUsed()?.Address.ColumnNumber ?? FirstParamCol;
            var keys = new List<string>();
            for (int c = FirstParamCol; c <= lastCol; c++) keys.Add(ws.Cell(KeyRow, c).GetString());

            if (!result.Meta.TryGetValue($"sheet:{ws.Name}:keys", out var expected) || expected != KeysHash(keys))
            {
                result.Problems.Add($"Sheet '{ws.Name}': the hidden mapping row was modified, so this sheet can't be imported. " +
                                    "This usually happens when Find & Replace runs on the whole sheet – select a cell range first next time.");
                continue;
            }

            var columns = new List<ParamInfo>();
            foreach (var k in keys)
            {
                var key = ParamKey.TryParse(k);
                if (key is null) { result.Problems.Add($"Sheet '{ws.Name}': invalid key '{k}'."); columns.Clear(); break; }
                var header = ws.Cell(HeaderRow, FirstParamCol + columns.Count).GetString();
                columns.Add(new ParamInfo { Key = key, Name = StripUnit(header) });
            }
            if (columns.Count == 0) continue;

            var sheet = new SheetData { Name = ws.Name, Columns = columns };
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? HeaderRow;
            for (int r = FirstDataRow; r <= lastRow; r++)
            {
                var uid = ws.Cell(r, UidCol).GetString();
                var values = new object?[columns.Count];
                for (int c = 0; c < columns.Count; c++) values[c] = ToObject(ws.Cell(r, FirstParamCol + c).Value);
                if (string.IsNullOrWhiteSpace(uid) && values.All(v => v is null)) continue;
                long.TryParse(Convert.ToString(values[0]), out var id);
                sheet.Rows.Add(new RowData { UniqueId = uid, ElementId = id, Values = values, Kinds = new CellKind[columns.Count] });
            }
            result.Sheets.Add(sheet);
        }
        return result;
    }

    private static object? ToObject(XLCellValue v) => v.Type switch
    {
        XLDataType.Blank => null,
        XLDataType.Number => v.GetNumber(),
        XLDataType.Boolean => v.GetBoolean() ? "Yes" : "No",
        XLDataType.Text => v.GetText(),
        XLDataType.DateTime => v.GetDateTime().ToString("yyyy-MM-dd"),
        _ => v.ToString(),
    };

    private static string StripUnit(string header)
    {
        var i = header.LastIndexOf(" [", StringComparison.Ordinal);
        return i > 0 && header.EndsWith(']') ? header[..i] : header;
    }
}
