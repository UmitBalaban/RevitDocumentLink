# 03 — UI Specification (original design)

The workflow (tabs → pick parameters → export / import → preview changes) follows the familiar round-trip pattern. The **visual design is our own**: do not imitate DiRoots colours, fonts, layout proportions, icons or wording.

## Design tokens (UI/Theme/Colors.xaml)

| Token | Light | Use |
|---|---|---|
| `Bg` | `#F6F7F9` | window background |
| `Surface` | `#FFFFFF` | panels, lists |
| `Border` | `#DADDE3` | 1px borders |
| `Text` | `#1C2330` | primary text |
| `TextMuted` | `#6B7380` | secondary text, status bar |
| `Accent` | `#0F7B6C` (teal) | primary buttons, selected tab underline, focus |
| `AccentHover` | `#0C6357` | |
| `KindInstance` | `#2E7D32` badge on `#E8F5E9` | instance parameter badge "INST" |
| `KindType` | `#8A5A00` badge on `#FFF1D6` | type parameter badge "TYPE" |
| `KindReadOnly` | `#5B6270` badge on `#ECEEF2` | read-only badge "RO" with a lock glyph |
| `Danger` | `#B3261E` | errors in preview |
| `Warning` | `#A15C00` | warnings in preview |

Font: Segoe UI 12 (body), 11 (status), Semibold 14 for section titles. Corner radius 4. Spacing grid 4/8/12/16.

Badges (pill shaped, 10px semibold) are how kinds are shown — **not** full-row background colour.

## Main window (min 1100×700, resizable, remembers size/position)

```
┌──────────────────────────────────────────────────────────────────────────┐
│ [logo] ParamBridge     Profile: [ Default        ▼] [💾][＋][✎][🗑] [⇪][⇩] │  ← header bar (Surface)
├──────────────────────────────────────────────────────────────────────────┤
│  Model │ Annotation │ Elements │ Schedules │ Spatial                       │  ← tabs (underline = Accent)
├───────────────────────┬──────────────────────────────────────────────────┤
│ Scope: (•)Model ( )View ( )Selection │                                   │
│ 🔍 search categories   │  AVAILABLE  [INST][TYPE][RO] 🔍     SELECTED  🔍  │
│ ☑ Air Terminals   124  │ ┌────────────────────────┐ ┌──────────────────┐ │
│ ☐ Ducts           860  │ │ Comments      INST Text│ │ Mark        INST │ │
│ ☑ Mechanical Eq.   47  │ │ Type Mark     TYPE Text│▶│ Comments    INST │ │
│   …                    │ │ Area          RO  Area │◀│ Type Mark   TYPE │ │
│                        │ │                        │⏩│                  │ │
│ [Isolate] [Reset] [Sel]│ └────────────────────────┘⏪└──────────────────┘ │
│                        │                                   [▲][▼]         │
├───────────────────────┴──────────────────────────────────────────────────┤
│ Sort: (•)By instance ( )By type          [ Import… ]  [ Export… ](Accent) │
├──────────────────────────────────────────────────────────────────────────┤
│ 2 categories · 171 elements · 63 parameters found · 3 selected            │  ← status (TextMuted)
└──────────────────────────────────────────────────────────────────────────┘
```

- Left pane width 300px, splitter between left pane and picker.
- Schedules tab: left pane is a schedule tree; the picker shows the schedule's fields in field order (Available = hidden + not-yet-selected fields).
- Spatial tab: left pane has a segmented control [Rooms | Spaces | Areas], area-scheme combo when Areas, and a list of levels with counts. TYPE toggle hidden.
- Elements tab: left pane shows "Load current selection" button + element list grouped by category.

## Changes preview window (modal, 1000×600)
Toolbar: `Showing: [All ▼]` (All / Will apply / Warnings / Errors), counters `✓ 120  ⚠ 3  ✗ 2`, search.
Grid columns: ☑ · Sheet · Id · Category · Type · Parameter · Old → New · Status/Reason. Old value muted, new value bold. Error rows have Danger text, checkbox disabled.
Footer: [Reset values] … [Cancel] [Apply selected (Accent)].

## Progress window (modal-less, topmost over Revit, 420×140)
Title line (e.g. "Reading values"), detail line ("Ducts · 412/860 elements"), determinate progress bar (Accent), [Cancel].

## Icons (Resources/Icons) — our own family style
Same visual language as our other add-ins (FilterManager / FilterHealth / FilterTransfer):
- **Transparent background, no tile.** Glyph sits in a 96 px grid with ~8 px margin.
- **Filled line icon:** outline + light fill. Light theme: outline `#105090` (secondary `#2060A0`), fill `#D0E0F0`. Dark theme: outline `#70B0E0`, fill `#204050`.
- **One accent detail** in orange: `#E07020` light / `#F0A050` dark (green `#208050` is reserved for "health/check" tools).
- Every icon ships as a **light + dark pair**: `bridge_light_16/32/96.png`, `bridge_dark_16/32/96.png`. `App.ApplyIcons()` picks the pair from `UIThemeManager.CurrentTheme` and re-applies on `ThemeChanged` (Revit 2024+).
- ParamBridge glyph: spreadsheet (left) + isometric cube = model (right) + orange two-way arrow underneath. 16 px variant drops inner grid lines.
- Preview: `assets/icon_preview.png`.
- Toolbar glyphs inside windows: Segoe Fluent Icons / Segoe MDL2 Assets: Save `E74E`, Add `E710`, Rename `E8AC`, Delete `E74D`, Import `E8B5`, Export `EDE1`, Isolate `E7B3`, Select `E8B3`, Up `E70E`, Down `E70D`.
