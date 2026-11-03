# 06 — Static analysis findings (Rea, DiRoots One 2.4.1.0)

Scope: **feature surface only** — what a user can see/do and which public libraries are used. No decompiled logic: the assembly is obfuscated and licensed, and its internals were deliberately not unpacked. Nothing here is to be copied; it only confirms the feature set described in 01.

## Packaging
- SheetLink is not a separate add-in: it lives inside `DiRoots.One.dll` (9.4 MB, .NET 8 / `net8.0-windows`, x64, IL-only, 19,923 types) under the namespace `DiRoots.One.SheetLink.*`. Type/method names are partly obfuscated (e.g. `A.\`2`), UI types keep readable names (needed by XAML).
- Shared libraries in the bundle: `DiRoots.Revit.DataCollectors.dll`, `DiRoots.One.Revit.dll`, `DiRoots.One.Commons.dll`, `DiRoots.WpfControls.dll`, `DiRoots.One.LanDict.dll` (localisation: EN/ES/FR/PT…), `DiRoots.One.Licensing.dll` + `LicenseSpring.dll`.

## Third-party libraries observed
| Library | Inferred purpose | Our choice |
|---|---|---|
| Syncfusion XlsIO, SfSpreadsheet, SfCellGrid | Excel read/write + in-app spreadsheet preview | ClosedXML (MIT) + WPF DataGrid |
| Google.Apis.Drive.v3 / Auth | Google Drive / Sheets export & import | v2, optional |
| CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors, M.E.DependencyInjection | MVVM, behaviours, DI | CommunityToolkit.Mvvm |
| NLog | logging | simple file logger |
| Newtonsoft.Json | profiles/settings | System.Text.Json |
| netDxf, Syncfusion Pdf/DocIO | other DiRoots One tools (not SheetLink) | – |

## Feature surface (from UI type names)
- Windows/controls: main window, progress window, export options, standard export options, replace confirmation, profile control + new profile, model categories, annotation categories, elements window, schedule window, spatial control, category / schedule / spatial / element navigators, manage parameters, parameter control, schedule parameters, preview tab control, custom progress bar.
- Other namespaces: `Profile`, `ProjectStandards`, `Models.TemplateInfo`, `Morta` (integration with the Morta platform), `Converters` (visibility converters), `Behaviors`.
- Member names indicating capabilities: hidden columns, keep formatting, data validation, worksheet regions, units conversion (internal ↔ display), instance/type parameter sorting, key-schedule support, UniqueId-based mapping, overwrite prompt, Google Drive import/export, Morta upload.

## Behaviour hints from localisation keys (paraphrased)
- Workbook stores mapping data in **one hidden column and one hidden row**; a modified structure blocks import, with a hint that Find & Replace over the whole sheet is the usual cause.
- Import errors: element not found; linked element can't be updated; property parameter not found; phase column missing / empty / phase not existing; value not in dropdown list; key schedule value not found; duplicate element on import.
- Type parameters: user is told all instances of a type need the same value.
- Progress messages: per worksheet, per category, per element, "collecting changes", "updating changes".
- Profiles: create, override-by-name confirmation, invalid profile file, permission problems on the profile folder.
- Export: to Excel folder or Google Drive, "open folder after export", existing worksheets → what to do, order by instance / by type, "Reset Values" in import preview.
- Public docs describe colour coding by kind (instance / type / read-only / missing) and an Instructions tab in the exported workbook.

## Not determined (would need decompilation — intentionally not done)
Exact hidden-row encoding, hashing, sort algorithms, how linked documents are traversed. Our own designs for these are in 04_EXCEL_FORMAT.
