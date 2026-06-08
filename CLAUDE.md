# SeriousView — project guide for Claude Code

Native cross-platform desktop markdown/code viewer (Avalonia, Skia, no WebView).
Open-source, Apache-2.0. A deliberate rewrite of an HTML/WebView markdown viewer.

## Commands

```bash
dotnet build SeriousView.sln -c Debug          # build
dotnet test  SeriousView.sln                   # unit + Headless UI tests
dotnet run --project src/SeriousView           # run (or: SeriousView <file>)
dotnet format SeriousView.sln                  # apply formatting (CI verifies)
```

Requires the **.NET 9 SDK**. Built and tested on Windows/Linux/macOS (see CI).

## Architecture (hold the line)

- **Three projects.** `SeriousView` (UI/WinExe) → references `SeriousView.Core`
  (pure `net9.0` library). `SeriousView.Tests` (xUnit + Avalonia.Headless).
  **See `ARCHITECTURE.md` for the full map and "where new code goes".**
- **Core has NO Avalonia dependency.** Pure logic + ports only (`Core/Abstractions`,
  `Core/Services`, `Core/Text`) — thin, no DDD layers. UI concerns (file dialog,
  theme, clipboard) are interfaces in `Core/Abstractions`, implemented in `SeriousView/Platform`.
- **UI is feature-sliced.** Code lives in `SeriousView/Features/<Name>` (Shell,
  Welcome, Viewer, …) with namespace = folder (`SeriousView.Features.<Name>`);
  cross-feature bits in `Shared/`, port implementations in `Platform/`, global
  styles in `Themes/`. View models use AvaloniaEdit's `TextDocument`; Core stays
  UI-free. CommunityToolkit.Mvvm source generators (`[ObservableProperty]`,
  `[RelayCommand]`) — VMs are `partial`. **No speculative ports/models ahead of a
  consumer (YAGNI): a feature brings its contract + impl + test in one commit.**
- **MVVM + DI.** Everything is resolved from the `ServiceProvider` built in
  `App.axaml.cs`. The window takes its VM via constructor injection.
- **Theming via tokens.** All chrome colors are `{DynamicResource ...Brush}`,
  defined in `Themes/Colors/{Light,Dark}.axaml` and wired through
  `Application` `ThemeDictionaries`. Never hard-code hex in views.
- **AvaloniaEdit bridge.** `TextEditor.Document` is bound from the VM;
  TextMate highlighting is a per-control concern handled by
  `Features/Viewer/EditorBehavior.cs` (installs TextMate, switches grammar by the
  `GrammarExtension` attached property, follows the theme, disposes the installation
  on `DetachedFromVisualTree`). Don't put editor wiring back into code-behind.
- **CPM.** All package versions are pinned in `Directory.Packages.props`
  (no `Version=` in `.csproj`, no floating). Shared MSBuild props in
  `Directory.Build.props`.
- **Compiled bindings** are on by default — every view / DataTemplate needs an
  `x:DataType`.

## Avalonia 11, not 12 (deliberate)

Avalonia **12 core is stable** (12.0.4, May 2026 — with large render-perf gains) and
`Avalonia.AvaloniaEdit 12.0.0` exists, but the rest of the viewer ecosystem
(Markdown.Avalonia, FluentAvaloniaUI) was **not confirmed stable on 12** as of mid-2026.
We stay on the 11.3.x line until those two ship stable 12 builds, then migrate. **Re-verify
Markdown.Avalonia + FluentAvalonia on 12 (NuGet) before proposing the upgrade** — the blocker
is the ecosystem, not Avalonia itself.

## Roadmap

Roadmap-driven, one milestone at a time; each feature is its own commit.
M1 (skeleton) + M2 (visual/UX) done, plus a visual-polish pass and a structure
refactor to feature-slices (`ARCHITECTURE.md`). **M3 (markdown rendering) done**:
Markdown.Avalonia `MarkdownScrollViewer` in `Features/Viewer/DocumentView`, a
preview/source toggle per markdown tab, code blocks via our AvaloniaEdit, links
hardened to http/https/mailto, and a pure `Core/Text/MarkdownPreprocessor` that
adds GitHub admonitions (`> [!NOTE]` → themed callouts via `AdmonitionBlockHandler`),
GFM task-list glyphs and footnotes. Renderer follows Light/Dark (auto FluentAvalonia
style — never set `MarkdownStyleName`). **M4 (TOC/outline) done**: collapsible left
sidebar (`Features/Viewer/OutlinePanel`) listing headings from pure
`Core/Text/MarkdownOutline`; clicking scrolls the source editor by line, or the preview
in place (visual-tree `BringIntoView` on `Heading1..6` controls — no public API exists).
**M5 (robust file ingestion) done**: pure `Core/Text` + `Core/Documents` loader —
encoding detection (BOM → strict UTF-8 → Windows-1251, via
`System.Text.Encoding.CodePages`; works under InvariantGlobalization), binary-file
detection, CR/CRLF→LF normalization, size limits (no TextMate >5 MB, don't load >50 MB);
`IFileReader.LoadAsync` returns a `FileLoadResult` (Text/Binary/TooLarge); guarded async
startup read; friendly error messages; a notice overlay for binary/too-large/empty;
status shows encoding · EOL. Backlog lives in `BACKLOG.md`.
**M6 (persistence) done**: a typed `Core/Settings/AppSettings` (theme + window + session) held by
`IAppSettingsService` (`AppSettingsService`) and persisted as one atomic `settings.json`
(`JsonSettingsStore` writes temp + `File.Replace`). Theme restored/applied at startup before the
first render; window size/position/maximized restored with off-screen re-centring
(`WindowPlacementValidator` against `Screens`) — note AppWindow's `ExtendsContentIntoTitleBar`
inflates the `Height` getter by the title-bar height, so we measure that chrome offset once and
compensate on save to avoid per-launch drift; session (open files + active tab) reopened at startup
when launched with no file arg (arg > session > welcome), **no `ISessionStore` port** (the holder is
the seam). Unhandled exceptions logged to `%AppData%/SeriousView/crash.log` (`Platform/CrashLogger`
+ pure `Core/Diagnostics/CrashLog`). Window icon (#9) deferred to a visual-polish pass.
**M7 (keyboard & editor controls) done**: a central tunnelling `KeyDown` dispatcher in `MainWindow`
maps shortcuts to VM commands and runs *before* the focused AvaloniaEdit (which otherwise swallows
letter-gestures via bubbling `KeyBindings`) — Ctrl+O/W, Ctrl+Tab/Shift+Tab tab nav, Ctrl+±/0 (+ NumPad)
& Ctrl+wheel font zoom, Ctrl+L line numbers, Alt+Z word-wrap, Ctrl+G go-to-line. Shared
`Shared/EditorOptions` (font/wrap/line-numbers, one instance across all tabs) drives the editor and
persists in `AppSettings.Editor`; a title-strip control cluster + the keyboard both mutate it. Caret
position shows in the status bar (relayed from `Caret.PositionChanged`); the source editor auto-focuses
on tab activation (must focus `Source.TextArea`, not the `TextEditor` wrapper). Go-to-line input lives
in the **status bar** (chrome): an overlay floated over the editor won't repaint over AvaloniaEdit's GPU
surface (sibling Border with IsVisible/Opacity/ZIndex and a Popup both failed) — see project memory.
Known gaps (deferred): `_underscore_` emphasis (use `*asterisks*`), Math/KaTeX,
Mermaid/diagrams, in-doc search, export, active-heading highlight on scroll.
Next: M8 (tabs & shell ergonomics). Feature spec source: `E:\Scripts\Markdown Viewer\CLAUDE.md`.

## Conventions

- Comments in English. Fix root causes, not symptoms.
- Each feature = one commit; commit messages end with a `Co-Authored-By` trailer.
- Don't commit `bin/`, `obj/` (covered by `.gitignore`).
- A feature isn't done without a test on its logic (Core unit or Headless UI).
