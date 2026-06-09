# SeriousView — Architecture

How the codebase is organised and **where new code goes**. Deliberately pragmatic: a thin core,
feature-sliced UI, and three projects — no Clean-Architecture ceremony, no speculative scaffolding.
(A 2026 modernisation audit rejected a heavier 4-project Clean-Architecture design as over-engineering
for an app of this size; we keep it light and grow by feature.)

## 1. Overview

Native cross-platform desktop **markdown / code viewer**. Avalonia 11.3 (Skia, no WebView), .NET 9,
MVVM (CommunityToolkit.Mvvm), DI (Microsoft.Extensions.DependencyInjection), Central Package Management.

```
            ┌─────────────────────────────┐
            │ SeriousView (UI, Avalonia)  │  Features/ · Platform/ · Shared/ · Themes/
            │   depends on ▼              │
            ├─────────────────────────────┤
            │ SeriousView.Core (net9.0)   │  pure logic + Abstractions (ports). NO Avalonia.
            └─────────────────────────────┘
            SeriousView.Tests mirrors both (Headless UI + unit).
```

## 2. Projects

| Project | Role | Rules |
|---|---|---|
| **SeriousView.Core** | Pure logic + ports (interfaces). `Abstractions/` (IFileReader, IFileDialogService, IThemeService+ThemeMode, ISettingsStore, IAppSettingsService, IRecentFilesStore), `Services/` (RecentFilesList, FileReader, AppSettingsService), `Text/` (TextMetrics, MarkdownFile/Link/Preprocessor/Outline, TextEncodingDetector, BinaryContent, LineEndings), `Documents/` (FileLoadResult, FileLimits), `Settings/` (AppSettings, WindowPlacement, SessionState, EditorSettings, WindowPlacementValidator), `Diagnostics/` (CrashLog). One BCL dep (`System.Text.Encoding.CodePages`) for Windows-1251. | **No Avalonia, no UI.** No DDD layers — keep it flat and small. |
| **SeriousView** (UI, WinExe) | Avalonia app. `Features/` (Shell, Welcome, Viewer, …), `Platform/` (Avalonia/IO port impls), `Shared/` (cross-feature VM base, converters, `EditorOptions` — shared editor font/wrap/line-numbers), `Themes/`, `App`. AssemblyName=SeriousView. | Talks to the outside world only through Core ports. |
| **SeriousView.Tests** | xUnit + Avalonia.Headless. Mirrors structure: `Core/`, `Features/`, `Platform/`. | Pure logic → `[Fact]`; UI/resources → `[AvaloniaFact]` + `TestAppBuilder`. |

## 3. Dependency rule

Arrows point inward only: **UI → Core**; Core depends on nothing (no Avalonia). The UI reaches files,
dialogs, theme, storage etc. **only via `Core/Abstractions` ports**, implemented in `Platform/`.
Never `using Avalonia` inside Core.

## 4. How to add a feature

1. Create `Features/<Name>/` with `<Name>View.axaml(.cs)` + `<Name>ViewModel.cs`
   (namespace `SeriousView.Features.<Name>`; VM derives `ViewModelBase` from `Shared/`).
2. Need the outside world (files, shell, clipboard, a renderer)? Add a **port** interface to
   `Core/Abstractions` **and its implementation in `Platform/`** (or pure logic in `Core`).
   **Create the port only when its real consumer exists** (see §7).
3. Register in DI (`App.axaml.cs ConfigureServices`, or a small `Add<Name>()` extension).
4. Add a mirrored test under `Tests/Features/<Name>/` (or `Core/` for pure logic).

**One feature = one commit** carrying View+VM + any port + implementation + test **together**.

## 5. Where the original viewer's features land (map, not code yet)

Source of features: `E:\Scripts\Markdown Viewer`. Implement incrementally per roadmap (M3+).

| Original domain | Lands in |
|---|---|
| Markdown rendering (GFM, tables, footnotes, admonitions) | **DONE (M3)** — a View control (`Features/Viewer/DocumentView` hosts Markdown.Avalonia's `MarkdownScrollViewer`) + pure `Core/Text/MarkdownPreprocessor` (GitHub alerts → `:::` containers, task lists → glyphs, footnotes) + `Features/Viewer/AdmonitionBlockHandler` (`IContainerBlockHandler`). **No `IMarkdownRenderer` port** — it's a control, not a swappable service (YAGNI, §7). |
| Code highlighting / decoration | `Features/Viewer` (EditorBehavior / TextMate) |
| Diagrams (Mermaid/PlantUML), Math (KaTeX) | port `IDiagramRenderer` + `Features/Viewer` |
| TOC / outline | **DONE (M4)** — pure `Core/Text/MarkdownOutline` (heading parse) + `Features/Viewer/OutlinePanel` sidebar; navigation scrolls the source editor by line, or the preview in place by walking the visual tree (`DocumentView`, no port). In-document **find is DONE (M9)** — pure `Core/Text/TextSearch` + a Ctrl+F find bar in `Features/Viewer/DocumentView` (source-only; an `IBackgroundRenderer` highlights matches). |
| Sync-scroll | behaviour in `Features/Viewer` (no port) |
| Export PDF/HTML | port `IExporter` + `Platform/` |
| Theme presets | `Themes/` (+ optional `IThemePresetProvider`) |
| Settings / window state | **DONE (M6)** — typed `Core/Settings/AppSettings` held by `IAppSettingsService` (`AppSettingsService`), persisted atomically via `ISettingsStore` as one `settings.json`. Theme + window placement + session restore. |
| Tabs / session restore | **DONE (M6)** — session (open files + active tab) is a field in `AppSettings`, saved on window close and restored at startup. **No `ISessionStore` port** — the holder is the seam (YAGNI, §7), mirroring how M3 skipped `IMarkdownRenderer`. |
| Live-reload | port `IDocumentWatcher` (FileSystemWatcher impl in `Platform/`) |
| Bookmarks | port `IBookmarkStore` |

Except where marked **DONE**, these ports/models do **not** exist yet — add each with its feature.
Note how markdown rendering landed **without** the speculatively-mapped `IMarkdownRenderer` port:
the renderer turned out to be a control, so the only Core addition was pure preprocessing logic.
Treat the remaining rows as direction, not committed contracts.

## 6. Conventions

- **Namespace = folder.** `SeriousView.Features.<Name>`, `SeriousView.Platform`, `SeriousView.Shared`,
  `SeriousView.Core.*`. (`App`/`Program` stay at root `SeriousView`.) Keeps IDE0130 happy.
- **avares://** uses the **AssemblyName** (`SeriousView`), not folder/namespace — `avares://SeriousView/Themes/...`.
  Don't rename the assembly without checking theme loading (`ThemeServiceTests.ColorTokens` is the canary).
- **MVVM**: `[ObservableProperty]`/`[RelayCommand]` source generators (VMs are `partial`). Compiled bindings
  on by default → every View/DataTemplate needs `x:DataType`.
- **DI**: everything resolved from the `ServiceProvider` in `App.axaml.cs`. Single window resolved directly
  (no ViewLocator yet — see §8).
- **Theming**: chrome colours are `{DynamicResource ...Brush}` tokens in `Themes/Colors/{Light,Dark}.axaml`;
  never hard-code hex in views.
- **CPM**: package versions in `Directory.Packages.props`; shared MSBuild props in `Directory.Build.props`.

## 7. YAGNI rule for this project

Do **not** create ports, domain models, or abstractions ahead of a real consumer. Introduce an abstraction
when there is an actual implementation **and** caller in the same commit (Rule of Three for duplication).
"Readiness to fill in" comes from **this document + feature folders**, not from speculative empty code.

## 8. ViewLocator / navigation (future)

Not needed yet — one window, resolved from DI. Introduce a DI-aware `ViewLocator` (`IDataTemplate`,
VM→View) and an `INavigationService` port only when the first navigable feature appears (e.g. a Settings
dialog or Search overlay).

## 9. Build & verify

```
dotnet build SeriousView.sln -c Debug      # build
dotnet test  SeriousView.sln               # unit + Headless UI (baseline: 28)
dotnet format SeriousView.sln              # CI verifies formatting
dotnet run --project src/SeriousView       # run (or: SeriousView <file>)
```
