# 09 — Product decisions (owner: Ümit Balaban)

| # | Decision | Date |
|---|---|---|
| 1 | Product name **DocumentLink** (repo `RevitDocumentLink`). | 2026-11-03 |
| 2 | Supported Revit versions: **2023, 2024, 2025, 2026** — all from one code base. | 2026-11-03 |
| 3 | UI language **English** (strings in `.resx`). | 2026-11-03 |
| 4 | Excel / Preview cell colours: editable instance = no fill, type = yellow, read-only = red, missing = dark grey. | 2026-11-03 |
| 5 | Ribbon: Revit **Add-Ins** tab, panel "DocumentLink". Icon family style as in FilterManager (light/dark pair). | 2026-11-03 |
| 6 | One Windows test machine with Revit 2023–2026 installed (same machine as the IFC/NWC add-in). Primary dev/test version 2025; every milestone smoke-tested in 2023, 2024 and 2026 as well. | 2026-11-03 |
| 7 | OpenXml/ClosedXML DLL clash with other add-ins on 2023/2024: tested on the other Revit versions during M2; if it occurs, merge/internalize the Excel libraries. | 2026-11-03 |
| 8 | Distribution: **in-house only** for now. No code signing, installer or Autodesk Store until that changes. | 2026-11-03 |
| 9 | Google Drive/Sheets, Morta, Panel tab: not in v1. | 2026-11-03 |
