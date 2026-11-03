using Autodesk.Revit.DB;

namespace DocumentLink.Infrastructure;

/// <summary>
/// Revit 2023–2026 API differences in one place. Use these helpers instead of the version-specific members.
/// REVIT2023 / REVIT2024 / REVIT2025 / REVIT2026 are defined by the csproj (RevitVersion property).
/// </summary>
public static class Compat
{
    /// <summary>ElementId as long. Revit 2024+: ElementId.Value; 2023: IntegerValue.</summary>
    public static long Id(this ElementId id)
    {
#if REVIT2023
        return id.IntegerValue;
#else
        return id.Value;
#endif
    }

    /// <summary>ElementId from long. The long constructor exists from Revit 2024.</summary>
    public static ElementId ToElementId(this long value)
    {
#if REVIT2023
        return new ElementId((int)value);
#else
        return new ElementId(value);
#endif
    }

    /// <summary>True when Revit's UI theme is dark (UIThemeManager exists from Revit 2024; 2023 is always light).</summary>
    public static bool IsDarkTheme()
    {
#if REVIT2023
        return false;
#else
        return Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark;
#endif
    }
}
