using System.Globalization;
using Autodesk.Revit.DB;
using ParamBridge.Core.Models;

namespace ParamBridge.Core.Revit;

/// <summary>Writes an Excel value into a Revit parameter (inside an open Transaction).</summary>
public static class ValueWriter
{
    public static void Write(Element e, ParamKey key, object? value, CultureInfo culture)
    {
        var doc = e.Document;

        if (key.Origin == ParamOrigin.Pseudo)
        {
            if (key.Id != "TYPE") throw new InvalidOperationException("Column is read-only.");
            ChangeType(e, Text(value, culture));
            return;
        }

        var p = ParamResolver.Find(e, key) ?? throw new InvalidOperationException("Parameter not found on element.");
        if (p.IsReadOnly) throw new InvalidOperationException("Parameter is read-only.");

        var text = Text(value, culture);
        var dataType = p.Definition.GetDataType();
        bool ok;

        switch (p.StorageType)
        {
            case StorageType.String:
                ok = p.Set(text);
                break;

            case StorageType.Integer:
                if (dataType == SpecTypeId.Boolean.YesNo)
                {
                    var yes = text.Equals("Yes", StringComparison.OrdinalIgnoreCase) || text == "1"
                              || text.Equals("Evet", StringComparison.OrdinalIgnoreCase) || text.Equals("True", StringComparison.OrdinalIgnoreCase);
                    ok = p.Set(yes ? 1 : 0);
                }
                else ok = p.Set(Convert.ToInt32(Number(value, text, culture)));
                break;

            case StorageType.Double:
                if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Empty value skipped for numeric parameter.");
                var d = Number(value, text, culture);
                if (dataType is not null && !dataType.Empty() && UnitUtils.IsMeasurableSpec(dataType))
                    d = UnitUtils.ConvertToInternalUnits(d, doc.GetUnits().GetFormatOptions(dataType).GetUnitTypeId());
                ok = p.Set(d);
                break;

            case StorageType.ElementId:
                ok = p.Set(ResolveId(doc, p, text));
                break;

            default:
                ok = p.SetValueString(text);
                break;
        }

        if (!ok) throw new InvalidOperationException($"Revit rejected value '{text}'.");
    }

    private static void ChangeType(Element e, string typeName)
    {
        var current = ParamResolver.GetType(e) as ElementType ?? throw new InvalidOperationException("Element has no type.");
        var candidates = e.GetValidTypes();
        var target = candidates.Select(id => e.Document.GetElement(id)).OfType<ElementType>()
            .FirstOrDefault(t => t.FamilyName == current.FamilyName && t.Name == typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' not found in family '{current.FamilyName}'.");
        e.ChangeTypeId(target.Id);
    }

    private static ElementId ResolveId(Document doc, Parameter p, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ElementId.InvalidElementId;
        var current = doc.GetElement(p.AsElementId());

        IEnumerable<Element> pool = current is not null
            ? new FilteredElementCollector(doc).OfClass(current.GetType())
            : p.Definition is InternalDefinition { BuiltInParameter: BuiltInParameter.PHASE_CREATED or BuiltInParameter.PHASE_DEMOLISHED }
                ? doc.Phases.Cast<Element>()
                : new FilteredElementCollector(doc).OfClass(typeof(Level));
        // TODO(M3): Material parameters with empty current value → OfClass(typeof(Material)).

        var match = pool.FirstOrDefault(x => x.Name == name)
            ?? throw new InvalidOperationException($"'{name}' is not a valid value. Choose one from the dropdown list.");
        return match.Id;
    }

    private static string Text(object? v, CultureInfo c) => v switch
    {
        null => "",
        double d => d.ToString(c),
        IFormattable f => f.ToString(null, c),
        _ => v.ToString() ?? "",
    };

    private static double Number(object? raw, string text, CultureInfo culture)
    {
        if (raw is double d) return d;
        if (raw is long l) return l;
        if (double.TryParse(text, NumberStyles.Float, culture, out var x)) return x;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return x;
        throw new InvalidOperationException($"'{text}' is not a number.");
    }
}
