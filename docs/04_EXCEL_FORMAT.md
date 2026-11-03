# 04 — Excel file contract

ParamBridge-generated workbooks are both human-friendly and machine-readable. Import relies on this contract; tamper detection protects it.

## 1. Sheets
| Sheet | Visibility | Content |
|---|---|---|
| one per category / schedule (or one "Data" sheet in single-sheet mode) | visible | data |
| `Instructions` | visible, first tab | how to edit, colour legend, rules (written by us) |
| `_lists` | **very hidden** | dropdown value lists for data validation (one column per list) |
| `_meta` | **very hidden** | key/value pairs (see §5) |

Sheet names: max 31 chars, strip `: \ / ? * [ ]`, make unique by suffix ` (2)`.

## 2. Data sheet layout
```
        A (hidden)        B            C          D        E        F …
row 1   ##PB              I|BIP:ID     I|BIP:CAT  I|BIP:FAM T|BIP:SYMBOL_NAME_PARAM  I|SHR:6f1c…  (hidden)
row 2   UniqueId          Element Id   Category   Family   Type     Comments    Length [mm] …    (header, bold, frozen)
row 3+  <uniqueid>        123456       Ducts      …        …        value       1250 …
```
- **A1** = literal `##PB` marker. **Column A** hidden = `Element.UniqueId`. **Row 1** hidden = *parameter keys*.
- Fixed columns B–E: `ID` (Element Id), `CAT`, `FAM`, `TYPE` — keys `I|PB:ID`, `I|PB:CAT`, `I|PB:FAM`, `I|PB:TYPE` (PB = pseudo, not real Revit parameters). Only `I|PB:TYPE` is editable (changes type, see 01 §6.3).
- Parameter key grammar: `<I|T>|<BIP:<BuiltInParameter name> | SHR:<guid> | NAM:<name> | SCH:<schedule field index>>`
- Header text = two lines `Name [unit]` / `Kind · Data type · Group` (08 §4).
- Values follow `08_VALUE_RULES.md` (names, labels, display units — no ids except the Element Id column).
- Freeze panes at B3; autofilter on row 2; column widths auto-fit (cap 60).

## 3. Cell kinds & styling (our palette — same colours as the UI, see 07 §2)
| Kind | Fill | Font | Locked |
|---|---|---|---|
| Editable instance value | `#E4F4EA` (option "tint instance cells", default on) | normal | no |
| Type parameter value | `#FFF3D3` | normal | no |
| Read-only value | `#FBE4E4` | italic `#8A3B39` | yes |
| Parameter missing on this element | `#D0D3D9` with value empty | – | yes |
| Linked-model element row | `#ECEEF2` whole row | italic | yes |
| Header row | `#0F7B6C` | bold white | yes |

When "Protect read-only cells" is on: unlocked = editable + type cells; `worksheet.Protect()` with allowed: select locked/unlocked, format columns, sort, autofilter. No password.

Header cell comment (note) contains: parameter kind, data type, origin, group, and for type params "Same value required for all instances of a type".

## 4. Values
- Numbers: exported as numeric cells in **display units**, header suffix ` [unit symbol]`. Rounded to the project's format-options accuracy only for display (`NumberFormat`), full precision stored.
- Yes/No → text "Yes"/"No" with dropdown list.
- ElementId params → referenced element name with dropdown list from `_lists`.
- Strings → text cell (`@` number format so Excel doesn't convert "001" to 1).
- Integers → numeric, format `0`.
- Empty/no value → empty cell.

## 5. `_meta` sheet
| Key | Example |
|---|---|
| `format` | `ParamBridge/1` |
| `revitVersion` | `2025` |
| `documentTitle` | `Project1.rvt` |
| `documentGuid` | ProjectInformation UniqueId (warn on import if different model) |
| `exportedAtUtc` | ISO-8601 |
| `culture` | `en-US` (number formats) |
| `sheet:<name>:hash` | SHA-256 over `row1 keys joined by \u001F` + `\u001E` + `column A values joined by \u001F` |

On import, recompute the hash per sheet. Mismatch on the **keys** part → refuse sheet (tamper). Mismatch only because rows were **deleted/re-ordered** → allowed (hash is advisory for rows: store a second hash `sheet:<name>:keys` for the keys only and treat that one as authoritative). Added rows with empty UniqueId → "new element" (Spatial only, v2) otherwise ⚠ ignored.

## 6. Instructions sheet (content outline, write in our own words)
1. What this file is and which model it came from (from `_meta`).
2. Colour legend (table using the exact fills above).
3. Rules: don't edit grey cells; type values must be consistent per type; don't insert columns before column B; don't modify hidden row 1/column A; restrict Find & Replace to a selected range.
4. How to import back: ParamBridge → Excel Sync → Import…
