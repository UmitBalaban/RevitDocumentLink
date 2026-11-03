# 05 — Instructions for the coding agent (DeepSeek or any other)

You are implementing **DocumentLink**, a Revit **2023–2026** add-in (English UI). Read, in order: `01_FEATURE_SPEC.md`, `02_ARCHITECTURE.md`, `03_UI_SPEC.md`, `07_UI_V2.md` (+ open `mockups/main-window.html`), `04_EXCEL_FORMAT.md`, `08_VALUE_RULES.md`. Where 03 and 07 differ, **07 wins**. A starter solution is in `src/` — extend it, don't restart it.

## Hard rules
1. **Original work only.** Do not copy, decompile, or paraphrase code, XAML, icons, Excel templates or UI text from DiRoots or any other commercial add-in. Implement from the specs and the public Revit API documentation.
2. Every Revit API call goes through `RevitTask.RunAsync` (see 02 §Threading). Never store `Element`/`Parameter`/`Document` in ViewModels.
3. One code base, four builds: `-p:RevitVersion=2023|2024` → net48, `2025|2026` → net8.0-windows (C# 12, nullable). `./build-all.ps1` must pass after every milestone. Version-specific API only via `Infrastructure/Compat.cs`.
4. No new NuGet packages beyond those listed in 02 without explaining why.
5. Keep user-facing strings in `Resources/Strings.resx` (English).
6. After each milestone: build (`dotnet build -c Release`), fix all errors and warnings you introduced, then **stop** and print: what you changed (file list), how to test it in Revit (steps), known gaps.
7. Use only API members that exist in **Revit 2023** or wrap the difference in `Compat` (e.g. `ElementId.Value` is 2024+, `UIThemeManager` is 2024+). Don't guess silently; leave a `// TODO(api):` comment if unsure.

## Milestones

### M0 — Build & load (starter code)
- Make all four builds pass with zero errors (`./build-all.ps1`). Add-Ins tab → panel "DocumentLink" → button "Excel Sync" opens the main window with five empty tabs.
- Accept: Revit starts without add-in errors; button opens/focuses one window; closing and reopening works.

### M1 — Model tab + parameter picker
- `ElementSource` (Model categories, scope: model / active view / selection), category list with counts, search.
- `ParameterScanner` builds the union of instance + type parameters for checked categories; picker with INST/TYPE/RO filters, search, add/remove, reorder.
- Accept: select Walls + Doors → see parameters; filters work; status bar counts correct.

### M1.5 — UI v2 shell
- Rebuild `MainWindow` per `07_UI_V2.md` and the mockup: custom title bar, tabs with badges, toolbar, four-column body, colour-coded parameter rows with glyphs, legend chips as filters, details pane, bottom bar. Light + dark colour dictionaries, theme follows Revit.
- Coverage % and data-type column from `ParameterScanner`; discipline filter; group-by; keyboard shortcuts.
- Accept: side-by-side with the mockup the structure and colours match; switching Revit to dark theme switches the window; 800 parameters scroll smoothly.

### M2 — Export to Excel
- `ValueReader`/`ValueWriter`/`RevitNames` per **08_VALUE_RULES** (names not ids, enum labels, display units), `WorkbookWriter` per 04 (mapping row, UniqueId column, styles, header notes, `_meta`, `Instructions`, dropdowns for Yes/No and Level/Phase/Material, protection).
- Export options dialog per 01 §5.1. Progress window with cancel.
- Accept: export 2 categories → 2 sheets + Instructions; hidden row/col present; numbers are numeric in display units; read-only cells locked.

### M3 — Import with preview & apply
- `WorkbookReader` + validation + tamper check, `DiffEngine`, `ChangesWindow`, `ValueWriter`, `ChangeApplier` (TransactionGroup, failure preprocessor, report).
- All rules of 01 §6.3.
- Accept (manual test script):
  1. Export Doors with Mark, Comments, Fire Rating (type), Width (type, read-only? depends on family), Level.
  2. In Excel change 3 Marks, 1 Comments, set Fire Rating for all doors of one type to "60", for another type change only one door's Fire Rating, change one Level to another level name, type into a grey cell.
  3. Import → preview shows 3+1 ✓, type change ✓ (1 change), conflicting type ✗, Level ✓, grey cell ✗ read-only.
  4. Apply → one Undo step in Revit reverts everything.
  5. Edit hidden row 1 in Excel → import refuses that sheet with the tamper message.

### M4 — Profiles
- `ProfileStore`, toolbar actions, import/export JSON, validation per 01 §4.

### M5 — Annotation, Elements, Spatial tabs + options
- Reuse M1–M3. Elements tab two-level (category → types) per 01 §10.3. Spatial: Rooms/Spaces/Areas, include unplaced, no Type filter, **create new rooms/spaces from new rows** (01 §10.6).
- Options on every tab: Include linked files, Export by Type ID, header click selects all of a kind, Reset (01 §10.1–10.2).

### M6 — Schedules tab
- `ScheduleReader`, field-order columns, header = column heading, key schedules, read-only calculated fields; one sheet per schedule.
- Two export modes: editable template vs keep formatting (not importable), scope whole model / active view, right-click open schedule (01 §10.5).

### M6.5 — Preview / Edit tab
- 07 §5: virtualized grid, kind colours, edit markers, Apply to model via Diff → Changes window, per-column filter.

### M7 — View tools & polish
- Isolate / reset / select in model, section box from selection with offset, highlight & zoom (01 §10.4), profiles with custom/network locations (01 §10.8), sort by instance / by type, remember window size, Turkish strings (`Strings.tr.resx`), log file, performance check (50k elements).

### M8 — Project standards (v1.1)
- 01 §10.7.

### Later (v2)
Panel tab (01 §10.9), Embedded preview/editor tab, room creation from Excel, Google Sheets, Revit 2022–2024 multi-targeting, signing & installer (WiX or Inno Setup).

## Testing without Revit
`Core/Excel` and `DiffEngine` must be unit-testable without Revit: add `tests/DocumentLink.Tests` (xUnit) in M2/M3 that round-trips a `SheetData` through `WorkbookWriter`/`WorkbookReader` and checks the tamper hash logic.

## Definition of done (v1)
M0–M7 accepted in Revit 2025 on a real project, plus a smoke test (open window, export, import one change) in Revit 2023, 2024 and 2026, (≥ 5,000 elements) with no unhandled exceptions in the log.
