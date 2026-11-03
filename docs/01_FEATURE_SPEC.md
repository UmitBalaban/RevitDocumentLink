# 01 — Functional Specification (DocumentLink)

Goal: a Revit add-in that exports model data to Excel, lets the user edit it, and imports the edits back into the model. Behaviour mirrors the well-known "Revit ⇄ Excel round-trip" workflow (as in DiRoots SheetLink), implemented from scratch with our own UI and icons.

Target: Revit 2025 and 2026 (.NET 8, WPF). Revit 2022–2024 (.NET Framework 4.8) is a later, optional milestone.

---

## 1. Entry point

- Revit's built-in **Add-Ins** tab, panel **DocumentLink**, push button **Excel Sync** (32px + 16px icons, tooltip: "Export model data to Excel, edit it, and update the model from the edited file.").
- Opens a **modeless** main window (user can keep working in Revit). All model reads/writes run through an `ExternalEvent` (see 02_ARCHITECTURE).
- Only one main window instance per Revit session; clicking again focuses it.

## 2. Main window – source tabs

The left/top area has five **source tabs**. Each tab decides *which elements* go to Excel. All tabs share the same parameter picker (§3), export (§5) and import (§6) logic.

| Tab | Source | Notes |
|---|---|---|
| **Model** | Model categories (`CategoryType.Model`) | Category tree with checkboxes, search box, "All categories" toggle. Option: scope = whole model / active view / current selection. |
| **Annotation** | Annotation categories (`CategoryType.Annotation`) | Same as Model. Include Sheets, Views, Tags, Text notes, Generic Annotations, Revisions etc. |
| **Elements** | Current Revit selection | "Load selection" button pulls `UIDocument.Selection`. Shows count "N elements, M categories". |
| **Schedules** | Existing `ViewSchedule`s (non-template, non-revision, non-internal-keynote) | Tree grouped by schedule category. Parameters = the schedule's fields (column header used as Excel header; the underlying `ParameterId` is stored in the mapping row). Option "Keep schedule formatting" (header text, field order, hidden fields excluded). Key schedules supported. Panel schedules: out of scope v1. |
| **Spatial** | Rooms / Spaces / Areas | Sub-selector: Rooms, Spaces, Areas (+ area scheme). Option "Include unplaced". No type parameters for these (hide the Type filter). Extra: "Create new rooms from Excel rows" (v2, see §6.6). |

Status bar at the bottom of each tab: `Categories selected: X | Elements: Y | Parameters found: Z | Parameters selected: W`.

### 2.1 Navigator (shared control, used on all tabs)
- Left list/tree of categories (or schedules / spatial types) with tri-state checkboxes and a search box.
- Element preview list for the checked items: Id, Category, Family, Type, Name/Mark.
- **Isolate**: temporarily isolate highlighted elements in the active view (`View.IsolateElementsTemporary`). **Reset isolation** button.
- **Select in model**: put highlighted elements into `UIDocument.Selection`.

## 3. Parameter picker (shared)

Two lists side by side: **Available parameters** ⇄ **Selected parameters** (add / remove / add all / remove all buttons, double-click moves).

- Available list is the **union** of parameters found on the elements in scope (instance params of the elements + type params of their types), de-duplicated by parameter identity (see 04_EXCEL_FORMAT §2 *Parameter key*).
- Each parameter row shows: Name, Kind badge, Storage/data type, Group, Origin (Built-in / Shared / Project / Family).
- Three **filter toggles** above the Available list: **Instance**, **Type**, **Read-only** (any combination; all on by default). Kind colours are defined in 03_UI_SPEC (our own palette).
- Search boxes on both lists.
- Selected list can be **re-ordered** (up / down buttons and drag-drop). Order = Excel column order.
- Fixed leading columns always exported (not removable): `Element Id`, `Category`, `Family`, `Type`. (`UniqueId` lives in the hidden column, see 04.)
- Sorting option for rows: **by instance** (Category → Family → Type → Mark/ElementId) or **by type** (group all instances of a type together).

## 4. Profiles (saved configurations)

- A profile stores: tab, checked categories/schedules/spatial kind, scope option, selected parameter keys in order, export options.
- Toolbar: profile dropdown, **Save**, **Save as new**, **Rename**, **Delete**, **Import profile (.json)**, **Export profile (.json)**.
- Stored as JSON in `%AppData%\DocumentLink\Profiles\` (one file per profile). Name validation: not empty, unique, no `\ : { } [ ] | ; < > ? \` ~` characters.
- When a profile references a parameter or category that does not exist in the current model, keep it in the file but show it greyed with a warning icon.

## 5. Export

### 5.1 Export options dialog
- Destination: **Excel file** (.xlsx) — choose file path. (Google Sheets: v2.)
- Workbook layout: **one worksheet per category** (default) or **single worksheet** (all categories; a `Category` column distinguishes rows). Schedules: one worksheet per schedule, sheet name = schedule name (sanitised to 31 chars, unique).
- If target file exists and already contains worksheets with the same names: ask **Replace sheets / Keep both (rename) / Cancel**.
- Checkbox **Open file after export** and **Open folder after export**.
- Checkbox **Include Instructions sheet** (default on).
- Checkbox **Protect read-only cells** (lock read-only & missing cells, sheet protection without password; **default off** — the reference workflow leaves sheets unprotected so users can sort/filter/format freely; tamper detection still protects the mapping).
- Units: export values in **project display units** (default) with numbers stored as numbers (not text) and the unit symbol shown only in the header, e.g. `Length [mm]`.

### 5.2 What gets written
See 04_EXCEL_FORMAT for the exact contract. Summary:
- Hidden row 1 = parameter keys, hidden column A = element `UniqueId`.
- Header row (row 2) = parameter display names (+ unit), frozen panes at B3.
- Cell colouring by kind: editable instance (no fill), type parameter, read-only, parameter missing on this element.
- Enum-like parameters (Yes/No, ElementId parameters such as Level / Phase / Material / Family Type, key-schedule values) get **Excel data-validation dropdown lists** built from the valid values in the model; list values are stored on a hidden `_lists` sheet.
- Progress window with cancel: "Reading values [i/n categories]", "[i/n elements]", "Writing sheet [i/n]".

### 5.3 Empty template export (Spatial tab)
"Export empty template" writes only the header + mapping row, so the user can fill new rooms (used by §6.6).

## 6. Import

### 6.1 Flow
1. **Import…** → pick .xlsx (or drag-drop onto the window).
2. Validate workbook: every data sheet must have the mapping row and UniqueId column. Otherwise show: *"This workbook was not created by DocumentLink or its hidden mapping row/column was changed."* and stop for that sheet.
3. For each row: find element by `UniqueId` (fallback: Element Id column if UniqueId cell empty and element exists – warn).
4. For each editable cell: convert Excel value → Revit value (units, Yes/No, ElementId lookup by name), compare with current model value. If different → a **Change**.
5. Show the **Changes preview** window (§6.2). Nothing is written until the user confirms.
6. Apply in **one** `TransactionGroup` with one `Transaction` per worksheet (so the user gets one undo step: `TransactionGroup.Assimilate`). Use a failure preprocessor that collects warnings instead of showing dialogs.
7. Show result summary: updated / skipped / failed counts, with a "Copy report" button and "Export report to Excel".

### 6.2 Changes preview window
- Grid: Sheet, Element Id, Category, Type, Parameter, Old value, New value, Status (✓ will apply / ⚠ warning / ✗ error + reason).
- Checkbox per row (all checked by default except errors) — user can untick changes.
- Filter: show only errors / warnings.
- Buttons: **Apply selected**, **Cancel**, **Reset values** (re-reads the model and recomputes the diff).

### 6.3 Rules
- Read-only parameters and "missing parameter" cells are **never** written; if the user changed one, it appears as ✗ "read-only".
- **Type parameters**: grouped by `TypeId`. If all rows of the same type carry the same new value → one change on the type. If rows disagree → ✗ conflict "Instances of type X have different values for type parameter P". (Rule mirrors the documented SheetLink caution.)
- `Type` column changes (changing an instance's family type) → allowed if the new value names an existing type of the same family (`Element.ChangeTypeId`). Changing a type's **Type Name** parameter renames the type; duplicate name → ✗.
- Elements from **linked models** may be exported (read-only, grey) but never imported (✗ "linked element").
- Element not found by UniqueId → ✗ "element not found in document".
- Phase Created / Phase Demolished: value must be the name of an existing phase; empty → ✗.
- ElementId-valued parameters (Level, Material, etc.): value matched by name among valid candidates; not found → ✗ "value not valid, choose from the dropdown".
- Number parsing uses the culture saved in the workbook's `_meta` sheet (see 04), not the OS culture.
- Empty cell on a string parameter → set to empty string. Empty cell on a numeric parameter → skip (warning) unless option "Empty clears value" is on.
- Groups/assemblies: changing parameters of group members is skipped with ⚠ unless the parameter is allowed to vary by group instance.

### 6.4 Structure tamper detection
If the hidden mapping row or UniqueId column was edited (e.g. by Find & Replace across the whole sheet), detect it (checksum in `_meta`, see 04 §5) and refuse that sheet with a clear explanation suggesting the user restrict Find & Replace to a cell range.

### 6.5 Schedules import
Same as above; parameter key comes from the schedule field's `ParameterId`. Calculated / combined / count fields are read-only. Key schedules: rows are key elements; changing a key name renames the key.

### 6.6 Spatial: create new rooms (v2)
Rows with empty UniqueId on a Rooms sheet + a valid Level value → create unplaced rooms (`Document.Create.NewRoom(Phase)`), then set their parameters. Shown as "➕ new" in the preview.

## 7. Embedded preview/editor (v2)
A "Preview / Edit" tab inside the main window showing the exact grid that will be exported, editable in-app, then "Apply to model" without going through Excel. Use a WPF `DataGrid` (no third-party spreadsheet control required).

## 8. Non-functional
- 50,000 elements × 30 parameters must export in < 60 s on a normal laptop: read all values inside one ExternalEvent call, cache `Definition` lookups, write Excel with ClosedXML in bulk (`InsertData` / `Cell.Value` per row, avoid per-cell styling — style ranges).
- UI never blocks Revit; long operations show a progress window with Cancel.
- Logging to `%AppData%\DocumentLink\Logs\yyyy-MM-dd.log`.
- Localisation-ready: all user strings in `Resources/Strings.resx` (English first; Turkish second).
- No telemetry, no licensing in v1.

## 9. Out of scope v1
Google Sheets, Panel schedules, embedded editor, room creation, Revit 2022–2024 build, project standards export (object styles, line weights, etc.).

---

## 10. Addendum — features confirmed from the public SheetLink user guide (Nov 2026)

These come from the publicly documented behaviour (docs.dirootsone.diroots.com, SheetLink pages: Model, Annotation, Elements, Schedules, Spatial, PanelLink, Profile). Implement them in our own way.

### 10.1 Options shown above every category/element/schedule list
- **Include linked files** (checkbox): also list/collect elements from loaded Revit links (`RevitLinkInstance.GetLinkDocument()`). Linked rows are exported read-only and never imported (§6.3). Sheet column `Source` = link name.
- **Export by Type ID** (checkbox): one row **per element type** instead of per instance. Rows carry the type's UniqueId; only type parameters (+ Type Name) are offered in the picker; import writes directly to the types, so the "same value for all instances" conflict rule doesn't apply.
- Header checkbox on the category list = select / deselect all visible categories.

### 10.2 Parameter picker additions
- Clicking a kind filter header (INST / TYPE / RO) **selects all parameters of that kind** in the Available list (in addition to filtering).
- **Reset** button: clears the category selection and both parameter lists.

### 10.3 Elements tab (refined)
Two-level selection: first a **category list**, then a **list of that category's types** with checkboxes; checked types define the elements (all instances of those types, respecting the scope Whole model / Active view / Current selection). The "Load current selection" button remains as a shortcut.

### 10.4 View tools (all tabs)
- **Isolate selection** — temporary hide/isolate of the chosen categories / elements in the active view; **Reset isolation**.
- **Section box from selection** — take the active 3D view (or duplicate it, user choice), set a section box around the chosen elements with a user offset (default 1 m); option "show only these elements" vs "show everything inside the box".
- **Highlight in model** (right-click on rooms/spaces/elements in the navigator) — select them in Revit and zoom (`UIDocument.ShowElements`). If they aren't visible in the active view, find a plan view of their level and open it (warn it may take time).
- **Open schedule** (right-click on a schedule in the Schedules tab) → `UIDocument.ActiveView = schedule`.

### 10.5 Schedules tab (refined)
- Scope: Whole model (all schedules) or Active view (schedules placed on the active sheet).
- Two export modes:
  1. **Editable (template)** — our standard workbook (04), importable.
  2. **Keep formatting** — export the schedule as it looks (`ViewSchedule.Export` to a temp delimited file, then styled into xlsx: headers, grouping, totals). Marked **not importable** (no `_meta` keys). Use this mode for schedules that include linked elements, with a warning.

### 10.6 Spatial tab (refined)
- Rooms / Spaces list with checkboxes, search, Include linked files.
- New rows in Excel create new rooms/spaces on import. Minimum required columns per new row: **Number, Name, Phase** (Level optional → unplaced). UniqueId and Element Id cells stay empty for new rows.
- "Export empty template" = header + mapping rows only.
- Preview marks these rows "➕ new". (Moves from v2 to v1, milestone M5.)

### 10.7 Project standards (v1.1)
- **Export project standards**: one workbook with sheets *Project Information* (editable), *Object Styles* (category, subcategory, line weight projection/cut, line colour, line pattern, material — read-only in v1.1), *Line Styles* (read-only), *Families* (family name, category, type count, in-place yes/no — read-only).
- **Import project standards**: only Project Information parameters are written back.

### 10.8 Profiles (refined)
- Saving asks for a name **and a location** (default `%AppData%\DocumentLink\Profiles`, but any folder incl. network share). A profile list remembers the paths of profiles from other locations (`profiles-index.json`).
- Import = browse to a profile file anywhere and add it to the list; Delete removes it from the list (and deletes the file only if it is in the default folder, after confirmation).
- A profile stores: tab, scope, include-links, export-by-type flag, selected categories/types/schedules/rooms, parameter keys in order.

### 10.9 Electrical panels (v2, "Panel" tab)
Panel list (search) → for the chosen panel two tables: **panel (electrical equipment) parameters** and **its circuits' parameters** (`ElectricalSystem` via `FamilyInstance.MEPModel.GetElectricalSystems()`), with INST/TYPE/RO filters. Export editable or keep-formatting (panel schedule look, not importable). Import updates panel + circuit parameters.

### 10.10 Out of scope for us
Morta integration. Google Drive/Sheets stays v2.
