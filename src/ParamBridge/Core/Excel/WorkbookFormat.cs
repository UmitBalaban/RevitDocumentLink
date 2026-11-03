using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using ParamBridge.Core.Models;

namespace ParamBridge.Core.Excel;

/// <summary>Constants of the workbook contract (docs/04_EXCEL_FORMAT.md).</summary>
public static class WorkbookFormat
{
    public const string Marker = "##PB";
    public const string FormatVersion = "ParamBridge/1";
    public const string MetaSheet = "_meta";
    public const string ListsSheet = "_lists";
    public const string InstructionsSheet = "Instructions";
    public const int KeyRow = 1, HeaderRow = 2, FirstDataRow = 3;
    public const int UidCol = 1, FirstParamCol = 2;

    public static readonly XLColor Header = XLColor.FromHtml("#0F7B6C");
    public static readonly XLColor TypeFill = XLColor.FromHtml("#FFF1D6");
    public static readonly XLColor ReadOnlyFill = XLColor.FromHtml("#ECEEF2");
    public static readonly XLColor ReadOnlyFont = XLColor.FromHtml("#5B6270");
    public static readonly XLColor MissingFill = XLColor.FromHtml("#D0D3D9");

    public static string KeysHash(IEnumerable<string> keys)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001F', keys)));
        return Convert.ToHexString(bytes);
    }

    public static string SafeSheetName(string name, ISet<string> used)
    {
        var bad = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var s = new string(name.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()).Trim();
        if (string.IsNullOrEmpty(s) || s.StartsWith('_')) s = "Sheet " + s;
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
