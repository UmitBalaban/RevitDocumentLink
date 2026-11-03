# 08 — Value rules: what goes into Excel, and how it comes back

**Golden rule:** a cell shows what the user sees in Revit's **Properties palette / schedule** — names, labels and numbers in project display units — never internal ids, enum integers or internal (feet/radian) units. Import must accept exactly what export wrote (round-trip safe) and resolve names back to the right Revit value.

The user should never have to ask for this. When a parameter type isn't covered below, follow the golden rule and add a row to this table.

## 1. By storage / data type

| Revit parameter | Export (cell) | Cell type | Import (write back) |
|---|---|---|---|
| Text (`StorageType.String`) | `AsString()`; null → empty | text (`@` format, keeps "001") | `Set(string)`; empty cell → empty string |
| Multiline text | same, line breaks kept (`\n`), cell wrap on | text | same |
| URL | same | text (no hyperlink) | same |
| Yes/No (`SpecTypeId.Boolean.YesNo`) | `Yes` / `No` | text + dropdown | Yes/No/True/False/1/0/Evet/Hayır → 1/0 |
| Integer spec (`SpecTypeId.Int.Integer`) | number | number `0` | `Set(int)` |
| **Integer enums** (e.g. Location Line, Duct Shape, Function, Structural Usage, Room Bounding as enum, Phase Status…) | **`AsValueString()` label** ("Wall Centerline"), never the integer | text + dropdown of valid labels | match label → set the integer via `SetValueString(label)`; if that fails, map label→int from the candidate list collected at export |
| Measurable double (length, area, volume, angle, slope, flow, velocity, pressure, temperature, electrical…) | value converted with the **project's FormatOptions unit** for that spec (`UnitUtils.ConvertFromInternalUnits`), full precision; header shows the unit symbol `Length [mm]` | number, `NumberFormat` matching project accuracy | `ConvertToInternalUnits` with the same unit; text with a unit suffix ("1250 mm") is parsed by `UnitFormatUtils.TryParse` |
| Number (unitless double) | number | number | `Set(double)` |
| Currency | number in display format | number `#,##0.00` | `Set(double)` |
| **Level** (`LEVEL_PARAM`, `FAMILY_LEVEL_PARAM`, `SCHEDULE_LEVEL_PARAM`, Base/Top Constraint, Reference Level…) | **level name** | text + dropdown of level names | name → `Level.Id`; unknown name = ✗ |
| Phase Created / Demolished | phase name | dropdown | name → Phase.Id |
| Material params | material name; `<By Category>` when invalid | dropdown | name → Material.Id; `<By Category>` → `ElementId.InvalidElementId` |
| Family type params (`ElementId` to an ElementType, e.g. nested type, Profile, Door type) | `Family: Type` (as Revit shows it) | dropdown of `Family: Type` valid for that param | parse `Family: Type` → type id (same category) |
| System Type (duct/pipe system type), Fitting, Insulation type | type name | dropdown | name → id within that class |
| View / Sheet references (View Template, Referencing Sheet…) | view or sheet name (`Number - Name` for sheets) | dropdown | name → id |
| Workset (`ELEM_PARTITION_PARAM`, stored as Integer WorksetId) | **workset name** | dropdown of user worksets | name → `WorksetId.IntegerValue` |
| Design Option | option `Set : Option` name or `Main Model` | read-only v1 | – |
| Category (`ELEM_CATEGORY_PARAM` etc.) | category name | read-only | – |
| Host, Room, From/To Room, Space | host: `Category : Name [Id]`; room/space: `Number - Name` | read-only | – |
| Image (`ElementId` to ImageType) | image name | read-only v1 | – |
| Any other `ElementId` param | `AsValueString()`; fallback referenced element `Name`; negative id (built-in category) → `Category.GetCategory(doc, id).Name` | text | name → id among elements of the **same class** as the current value; ambiguous name = ✗ "ambiguous, n matches" |
| Calculated / formula-driven / reporting | value as displayed | read-only (red) | never written |

Pseudo columns: `Element Id` = the integer id (this one IS an id, by design), `Category` = category name, `Family` = family name, `Type` = type name (editable → changes type), optional `Family and Type` = `Family: Type`.

## 2. Number formatting & culture
- Numbers are written as real numbers; Excel shows them in the user's locale. We never write numbers as text.
- Rounding only through `NumberFormat` (display) using the project's accuracy for that spec, e.g. accuracy 1 mm → `0`, 0.01 m² → `0.00`.
- On import a text cell containing a number is parsed with the workbook culture (`_meta.culture`), then invariant culture, then `UnitFormatUtils.TryParse(units, spec, text)`.

## 3. Comparison (diff) rules
- Doubles: equal when |a−b| ≤ max(1e-9, half of the display accuracy) in display units — so untouched cells never show as "changed" because of rounding.
- Text: ordinal, but treat `null` and empty as equal; normalise `\r\n` → `\n`.
- Names (Level, Material…): compare by resolved id, not by string (renaming a level in Revit after export must not create false changes).

## 4. Header row content
Row 2 (visible header) is two lines (rich text, second line smaller and grey):
```
Box Height [mm]
Type · Length · Dimensions
```
= name [unit] / kind · data type · parameter group. The header note (comment) adds origin (Built-in name / Shared GUID / Project) and "same value for all elements of a type" for type params.

## 5. Tests (must exist from M2)
Unit tests in `tests/DocumentLink.Tests` for the pure parts (culture parsing, rounding tolerance, `Family: Type` parsing). Manual Revit test list in M2/M3 must include: Level, Base/Top Constraint, Phase, Material, Workset, an integer enum (Wall Location Line), Yes/No, a length in a mm project and a feet-inch project, an area, an angle, a duct flow (L/s and CFM), Family-type parameter, Host (read-only), Room `Number - Name`.
