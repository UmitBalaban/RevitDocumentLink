using ClosedXML.Excel;
using DocumentLink.Core.Models;

namespace DocumentLink.Core.Excel;

/// <summary>Constants of the workbook contract (docs/04_EXCEL_FORMAT.md).</summary>
public static class WorkbookFormat
{
    public const string Marker = "##DL";
    public const string FormatVersion = "DocumentLink/1";
    public const string MetaSheet = "_meta";
    public const string ListsSheet = "_lists";
    public const string InstructionsSheet = "Instructions";
    public const int KeyRow = 1, HeaderRow = 2, FirstDataRow = 3;
    public const int UidCol = 1, FirstParamCol = 2;

    public static readonly XLColor Header = XLColor.FromHtml("#0F7B6C");
    // docs/04_EXCEL_FORMAT.md §3 — editable instance cells have NO fill.
    public static readonly XLColor TypeFill = XLColor.FromHtml("#FFE8A3");     // editable type value
    public static readonly XLColor ReadOnlyFill = XLColor.FromHtml("#F7C1C1"); // read-only (family/type names, calculated…)
    public static readonly XLColor ReadOnlyFont = XLColor.FromHtml("#5A1F1D");
    public static readonly XLColor MissingFill = XLColor.FromHtml("#BFBFBF");  // parameter does not exist for this element
    public static readonly XLColor IdFill = XLColor.FromHtml("#F2F2F2");       // fixed Element Id column

    public static string KeysHash(IEnumerable<string> keys)
    {
        return DocumentLink.Infrastructure.TextUtil.Sha256Hex(string.Join("\u001F", keys));
    }

    public static string SafeSheetName(string name, ISet<string> used)
    {
        var bad = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var s = new string(name.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()).Trim();
        if (string.IsNullOrEmpty(s) || s.StartsWith("_")) s = "Sheet " + s;
        if (s.Length > 31) s = s[..31];
        var candidate = s;
        for (int i = 2; used.Contains(candidate) || candidate == InstructionsSheet; i++)
        {
            var suffix = $" ({i})";
            candidate = (s.Length + suffix.Length > 31 ? s[..(31 - suffix.Length)] : s) + suffix;
        }
        used.Add(candidate);
        return candidate;
    }

    public static string KindLabel(ParamInfo p) =>
        p.Key.Origin == ParamOrigin.Pseudo ? "Fixed column"
        : $"{(p.Scope == ParamScope.Type ? "Type" : "Instance")} parameter{(p.IsReadOnly ? " (read-only)" : "")}";
}
