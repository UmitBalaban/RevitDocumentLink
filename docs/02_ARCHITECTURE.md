# 02 — Architecture

## Solution layout

```
src/
  ParamBridge.sln
  ParamBridge/                      single add-in project (net8.0-windows, WPF)
    ParamBridge.csproj
    ParamBridge.addin               Revit manifest (copied to Addins folder)
    App.cs                          IExternalApplication: ribbon, ExternalEvent host
    Commands/
      OpenMainWindowCommand.cs      IExternalCommand → shows/focuses the modeless window
    Infrastructure/
      RevitTask.cs                  async wrapper around ExternalEvent (await RevitTask.RunAsync(app => ...))
      Log.cs                        tiny file logger
      ProfileStore.cs               JSON profiles in %AppData%\ParamBridge\Profiles
    Core/
      Models/                       pure POCOs, no Revit types except ids-as-long
        ParamKey.cs                 identity of a parameter (BuiltIn / Shared GUID / Project-or-Family name) + Instance|Type
        ParamInfo.cs                display info for picker (name, kind, group, data type, origin, isReadOnly)
        ExportRequest.cs            what the UI asks to export
        SheetData.cs                in-memory table: columns (ParamInfo) × rows (RowData)
        CellChange.cs               one diff item for the preview window
        Profile.cs
      Revit/                        everything that touches Autodesk.Revit.*  (must run on Revit API thread)
        ElementSource.cs            collects elements per tab (categories / selection / schedule / spatial)
        ParameterScanner.cs         builds the available parameter union
        ValueReader.cs              Parameter → (object excelValue, CellKind)
        ValueWriter.cs              (excelValue) → Parameter.Set with unit conversion & lookups
        ScheduleReader.cs           schedule fields → ParamInfo list
        ChangeApplier.cs            transaction group, failure preprocessor, results
      Excel/
        WorkbookWriter.cs           SheetData → .xlsx (ClosedXML) following 04_EXCEL_FORMAT
        WorkbookReader.cs           .xlsx → SheetData + validation (mapping row, checksum)
        ExcelStyles.cs              colour constants for cell kinds
      DiffEngine.cs                 SheetData(excel) vs current model → List<CellChange>
    UI/
      Theme/Colors.xaml, Controls.xaml   our own design tokens & styles
      MainWindow.xaml(.cs)          tabs + picker + toolbar
      ChangesWindow.xaml(.cs)       preview of import diff
      ProgressWindow.xaml(.cs)
      ViewModels/
        MainViewModel.cs
        SourceTabViewModel.cs       base for the five tabs
        ParameterPickerViewModel.cs
        ChangesViewModel.cs
    Resources/
      Icons/*.png                   our own icons (16, 32)
      Strings.resx
```

Keep it **one project** for v1 (simpler deployment). If Revit 2022–2024 support is added later, split `Core/Models` + `Core/Excel` + `DiffEngine` into a `netstandard2.0` library and multi-target the add-in.

## Threading model (most important rule)

- The WPF main window is **modeless** and runs on Revit's UI thread, but Revit API calls are only valid inside an API context. Therefore **every** Revit API access goes through `RevitTask.RunAsync(Func<UIApplication, T>)`, which raises an `ExternalEvent` and completes a `TaskCompletionSource` from inside `IExternalEventHandler.Execute`.
- Never hold `Element`, `Parameter` or `Document` objects in ViewModels. Hold `long` element ids, `string` UniqueIds and `ParamKey`s; re-resolve inside RevitTask.
- Excel I/O (ClosedXML) runs on a background thread (`Task.Run`) — it doesn't need Revit.
- Progress reporting from inside the ExternalEvent: Revit is busy, so the WPF dispatcher can't repaint. Approach: chunk work (e.g. 2,000 elements per ExternalEvent call) and report progress between chunks. Cancellation is checked between chunks.

## Data flow

```
Export:  UI selection ─► RevitTask: ElementSource + ParameterScanner + ValueReader ─► SheetData
         SheetData ─► Task.Run: WorkbookWriter ─► .xlsx

Import:  .xlsx ─► Task.Run: WorkbookReader (+validation) ─► SheetData(excel)
         RevitTask: DiffEngine (reads current values with ValueReader) ─► List<CellChange>
         ChangesWindow (user ticks) ─► RevitTask: ChangeApplier (ValueWriter in TransactionGroup) ─► report
```

## Revit API notes (2025/2026)

- `ElementId.Value` (long). Never use `IntegerValue` (removed).
- Units: `param.GetUnitTypeId()` gives the internal unit; display unit from `doc.GetUnits().GetFormatOptions(param.Definition.GetDataType()).GetUnitTypeId()`; convert with `UnitUtils.ConvertFromInternalUnits / ConvertToInternalUnits`. Only for `UnitUtils.IsMeasurableSpec(dataType)`. Symbol: `LabelUtils.GetLabelForSymbol(formatOptions.GetSymbolTypeId())` when symbol is not empty, else `LabelUtils.GetLabelForUnit(unitTypeId)`.
- Yes/No: `dataType == SpecTypeId.Boolean.YesNo`, storage Integer 0/1 → export as "Yes"/"No".
- ElementId parameters: export the referenced element's `Name` (Level, Material, Phase…); write by name lookup among `FilteredElementCollector(doc).OfClass(<same class as current value's element>)`.
- Parameter identity:
  - Built-in: `(param.Definition as InternalDefinition).BuiltInParameter != INVALID` → `BIP:<enum name>`
  - Shared: `param.IsShared` → `SHR:<GUID>`
  - otherwise → `NAM:<definition name>` (project/family parameter, name-based)
  - Prefix with `I|` or `T|` for instance/type.
- Resolve a key on an element: BIP → `element.get_Parameter(bip)`; SHR → `element.get_Parameter(guid)`; NAM → `element.LookupParameter(name)`.
- Read-only: `param.IsReadOnly` **or** element is in a linked document **or** element is in a group and parameter doesn't vary by group.
- Schedules: `ViewSchedule.Definition.GetFieldOrder()` → `ScheduleField.ParameterId` (BuiltIn when negative → `BuiltInParameter`, else `SharedParameterElement` / `ParameterElement`), `ColumnHeading` as header, `IsHidden` to skip, `FieldType` (Instance / ElementType / Room / FromRoom / ToRoom / Formula / Count…). Elements of a schedule: `new FilteredElementCollector(doc, schedule.Id)`.
- Isolation: `view.IsolateElementsTemporary(ids)` / `view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate)` inside a Transaction.
- Failures: implement `IFailuresPreprocessor`, delete warnings (`FailureSeverity.Warning`) and record them; for errors, `ResolveFailure` if possible else roll back that transaction and report.

## Dependencies (NuGet)

| Package | Why |
|---|---|
| `Nice3point.Revit.Api.RevitAPI` / `RevitAPIUI` (2025.*, 2026.*) | Revit references without a local Revit install; `ExcludeAssets=runtime` |
| `ClosedXML` (0.104.x) | read/write .xlsx, data validation, styling, protection. MIT licensed |
| `CommunityToolkit.Mvvm` (8.x) | `ObservableObject`, `RelayCommand` |

ClosedXML pulls `DocumentFormat.OpenXml`; Revit 2025 itself ships an older OpenXml in some setups — if a version conflict appears at runtime, isolate with an `AssemblyLoadContext` (Revit 2026 supports add-in isolation via the `.addin` manifest) or use the same version Revit ships. Ajan bunu kabul testinde doğrulamalı.

## Error handling & logging
- Every RevitTask call wraps exceptions and returns them to the awaiting UI code, which shows a single friendly dialog + writes the stack trace to the log.
- Never let an exception escape `IExternalEventHandler.Execute` or `IExternalApplication.OnStartup`.
