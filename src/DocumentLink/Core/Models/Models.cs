namespace DocumentLink.Core.Models;

public enum ParamScope { Instance, Type }

public enum ParamOrigin { BuiltIn, Shared, ProjectOrFamily, Pseudo }

public enum CellKind { Editable, Type, ReadOnly, Missing, Linked }

public enum SourceTab { Model, Annotation, Elements, Schedules, Spatial }

public enum ElementScope { WholeModel, ActiveView, Selection }

/// <summary>
/// Stable identity of a parameter, serialised as "I|BIP:ALL_MODEL_MARK", "T|SHR:guid", "I|NAM:Name", "I|DL:ID".
/// </summary>
public sealed record ParamKey(ParamScope Scope, ParamOrigin Origin, string Id)
{
    public static readonly ParamKey ElementId = new(ParamScope.Instance, ParamOrigin.Pseudo, "ID");
    public static readonly ParamKey Category = new(ParamScope.Instance, ParamOrigin.Pseudo, "CAT");
    public static readonly ParamKey Family = new(ParamScope.Instance, ParamOrigin.Pseudo, "FAM");
    public static readonly ParamKey TypeName = new(ParamScope.Instance, ParamOrigin.Pseudo, "TYPE");
    public static IReadOnlyList<ParamKey> Fixed { get; } = new[] { ElementId, Category, Family, TypeName };

    public override string ToString()
    {
        var s = Scope == ParamScope.Instance ? "I" : "T";
        var o = Origin switch
        {
            ParamOrigin.BuiltIn => "BIP",
            ParamOrigin.Shared => "SHR",
            ParamOrigin.ProjectOrFamily => "NAM",
            _ => "DL",
        };
        return $"{s}|{o}:{Id}";
    }

    public static ParamKey? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 5 || text[1] != '|') return null;
        var scope = text[0] switch { 'I' => ParamScope.Instance, 'T' => ParamScope.Type, _ => (ParamScope?)null };
        var colon = text.IndexOf(':');
        if (scope is null || colon < 0) return null;
        var origin = text[2..colon] switch
        {
            "BIP" => ParamOrigin.BuiltIn,
            "SHR" => ParamOrigin.Shared,
            "NAM" => ParamOrigin.ProjectOrFamily,
            "DL" => ParamOrigin.Pseudo,
            _ => (ParamOrigin?)null,
        };
        return origin is null ? null : new ParamKey(scope.Value, origin.Value, text[(colon + 1)..]);
    }
}

/// <summary>Picker row / Excel column description.</summary>
public sealed class ParamInfo
{
    public required ParamKey Key { get; init; }
    public required string Name { get; init; }
    public string DataType { get; init; } = "";
    public string Group { get; init; } = "";
    public bool IsReadOnly { get; init; }
    /// <summary>Display unit symbol, e.g. "mm"; empty for unitless.</summary>
    public string Unit { get; init; } = "";
    public string Header => string.IsNullOrEmpty(Unit) ? Name : $"{Name} [{Unit}]";
    public ParamOrigin Origin => Key.Origin;
    public ParamScope Scope => Key.Scope;
}

public sealed class RowData
{
    public required string UniqueId { get; init; }
    public long ElementId { get; init; }
    /// <summary>Values aligned with SheetData.Columns. Value is string, double, int or null.</summary>
    public required object?[] Values { get; init; }
    public required CellKind[] Kinds { get; init; }
}

/// <summary>One worksheet worth of data, independent of Revit and Excel.</summary>
public sealed class SheetData
{
    public required string Name { get; init; }
    public required List<ParamInfo> Columns { get; init; }
    public List<RowData> Rows { get; } = new();
    /// <summary>Column index → allowed values (dropdown lists).</summary>
    public Dictionary<int, List<string>> Lists { get; } = new();
}

public enum ChangeStatus { WillApply, Warning, Error }

public sealed class CellChange
{
    public required string Sheet { get; init; }
    public required string UniqueId { get; init; }
    public long ElementId { get; init; }
    public string Category { get; init; } = "";
    public string TypeName { get; init; } = "";
    public required ParamKey Key { get; init; }
    public required string ParameterName { get; init; }
    public string OldValue { get; init; } = "";
    public string NewValue { get; init; } = "";
    public object? RawNewValue { get; init; }
    public ChangeStatus Status { get; set; }
    public string Reason { get; set; } = "";
    public bool IsSelected { get; set; } = true;
    /// <summary>For type parameters: the element type id the change is applied to.</summary>
    public long TargetTypeId { get; init; }
}

public sealed class ExportRequest
{
    public SourceTab Tab { get; init; }
    public ElementScope Scope { get; init; }
    public List<long> CategoryIds { get; init; } = new();
    public List<long> ScheduleIds { get; init; } = new();
    public List<long> ElementIds { get; init; } = new();
    public List<ParamKey> Parameters { get; init; } = new();
    public bool SortByType { get; init; }
    public bool SingleSheet { get; init; }
}

public sealed class Profile
{
    public string Name { get; set; } = "Default";
    public SourceTab Tab { get; set; }
    public ElementScope Scope { get; set; }
    /// <summary>Category BuiltInCategory names or schedule names (names survive across models, ids don't).</summary>
    public List<string> Sources { get; set; } = new();
    public List<string> ParameterKeys { get; set; } = new();
    public bool SortByType { get; set; }
    public bool SingleSheet { get; set; }
}
