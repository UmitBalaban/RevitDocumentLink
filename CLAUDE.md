# ParamBridge — notes for Claude Code

Revit 2025/2026 add-in (C# 12, .NET 8, WPF) that round-trips model data to Excel. Specs live in `docs/`; work plan and acceptance tests in `docs/05_AGENT_INSTRUCTIONS.md` — follow its milestones in order and stop after each one.

## Commands (run on Windows)
- Build: `dotnet build src/ParamBridge.sln -c Release` (deploys to `%AppData%\Autodesk\Revit\Addins\2025\` automatically)
- Build for Revit 2026: `dotnet build src/ParamBridge.sln -c Release -p:RevitVersion=2026`
- Build without deploying (e.g. while Revit is running and DLLs are locked): add `-p:DeployToRevit=false`
- Tests (from M2 on): `dotnet test tests/ParamBridge.Tests`
- Runtime log: `%AppData%\ParamBridge\Logs\<date>.log` — read it after every manual Revit test.

## Rules
- Original work only: never copy or decompile DiRoots (or any other vendor's) code, XAML, icons, texts or templates. The `docs/` specs are the source of truth.
- Every Revit API call goes through `Infrastructure/RevitTask.RunAsync`; ViewModels hold ids/UniqueIds/ParamKeys, never `Element`/`Parameter`/`Document`.
- `Core/Excel` and `Core/Models` must stay free of Revit types so they are unit-testable.
- Revit 2025 API: `ElementId.Value` (long), `ForgeTypeId` units, no `IntegerValue`, no `DisplayUnitType`.
- UI follows `docs/07_UI_V2.md` and `docs/mockups/main-window.html` (colours = its CSS variables, light + dark). No third-party UI control libraries.
- The user wants to be consulted on design decisions: when the spec leaves a visible UI choice open, ask before building it.
- **Values must be exported the way Revit's Properties palette shows them** — level/phase/material/workset/type *names*, enum *labels*, numbers in project display units — never ids or internal units. `docs/08_VALUE_RULES.md` is binding; extend its table for any parameter type it doesn't cover instead of guessing.
- Revit must be closed for the deploy copy to succeed; if a build fails with "file in use", rebuild with `-p:DeployToRevit=false` and tell the user.
- You cannot click inside Revit yourself: after each milestone, give the user a short numbered manual test list and ask for the result and the log.
