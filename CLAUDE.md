# DocumentLink — notes for Claude Code

Revit **2023–2026** add-in (C# 12, WPF; .NET Framework 4.8 for 2023/2024, .NET 8 for 2025/2026), UI in English, that round-trips model data to Excel. Specs live in `docs/` (product decisions: `docs/09_DECISIONS.md`); work plan and acceptance tests in `docs/05_AGENT_INSTRUCTIONS.md` — follow its milestones in order and stop after each one.

## Commands (run on Windows)
- Build one version: `dotnet build src/DocumentLink/DocumentLink.csproj -c Release -p:RevitVersion=2025` (2023 | 2024 | 2025 | 2026; default 2025). Deploys to `%AppData%\Autodesk\Revit\Addins\<ver>\`.
- Build all four: `./build-all.ps1` (add `-NoDeploy` while Revit is open). **Every milestone must build for all four versions.**
- Build without deploying (e.g. while Revit is running and DLLs are locked): add `-p:DeployToRevit=false`
- Tests (from M2 on): `dotnet test tests/DocumentLink.Tests`
- Runtime log: `%AppData%\DocumentLink\Logs\<date>.log` — read it after every manual Revit test.

## Rules
- Original work only: never copy or decompile DiRoots (or any other vendor's) code, XAML, icons, texts or templates. The `docs/` specs are the source of truth.
- Every Revit API call goes through `Infrastructure/RevitTask.RunAsync`; ViewModels hold ids/UniqueIds/ParamKeys, never `Element`/`Parameter`/`Document`.
- `Core/Excel` and `Core/Models` must stay free of Revit types so they are unit-testable.
- Version differences go through `Infrastructure/Compat.cs` (`id.Id()`, `long.ToElementId()`, `Compat.IsDarkTheme()`); never call `ElementId.Value`/`IntegerValue`/`new ElementId(long)` directly. Use `#if REVIT2024_OR_GREATER` etc. only inside Compat or for whole features. `ForgeTypeId` units everywhere (available since 2022), never `DisplayUnitType`.
- Code must compile on .NET Framework 4.8: no `string.Contains(string, StringComparison)`, `KeyValuePair` deconstruction, `Convert.ToHexString`, `SHA256.HashData`, `string.Join(char, …)` — use `Infrastructure/TextUtil.cs`. PolySharp provides records/init/required/ranges.
- UI follows `docs/07_UI_V2.md` and `docs/mockups/main-window.html` (colours = its CSS variables, light + dark). No third-party UI control libraries.
- The user wants to be consulted on design decisions: when the spec leaves a visible UI choice open, ask before building it.
- **Values must be exported the way Revit's Properties palette shows them** — level/phase/material/workset/type *names*, enum *labels*, numbers in project display units — never ids or internal units. `docs/08_VALUE_RULES.md` is binding; extend its table for any parameter type it doesn't cover instead of guessing.
- Revit must be closed for the deploy copy to succeed; if a build fails with "file in use", rebuild with `-p:DeployToRevit=false` and tell the user.
- You cannot click inside Revit yourself: after each milestone, give the user a short numbered manual test list and ask for the result and the log.
