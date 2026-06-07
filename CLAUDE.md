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

The viewer ecosystem (Markdown.Avalonia, FluentAvaloniaUI) is stable only on the
11.x line as of mid-2026; on 12 it is alpha/preview. We start on 11.3.17 and plan
to migrate to 12 once that ecosystem stabilises. **Re-verify the ecosystem before
proposing an upgrade to 12.**

## Roadmap

Roadmap-driven, one milestone at a time; each feature is its own commit.
M1 (skeleton) + M2 (visual/UX) done, plus a visual-polish pass and a structure
refactor to feature-slices (`ARCHITECTURE.md`). **M3 (markdown rendering) done**:
Markdown.Avalonia `MarkdownScrollViewer` in `Features/Viewer/DocumentView`, a
preview/source toggle per markdown tab, code blocks via our AvaloniaEdit, links
hardened to http/https/mailto, and a pure `Core/Text/MarkdownPreprocessor` that
adds GitHub admonitions (`> [!NOTE]` → themed callouts via `AdmonitionBlockHandler`),
GFM task-list glyphs and footnotes. Renderer follows Light/Dark (auto FluentAvalonia
style — never set `MarkdownStyleName`). Known gaps (deferred): `_underscore_` emphasis
(use `*asterisks*`), Math/KaTeX, Mermaid/diagrams, TOC, in-doc search, export.
Next: M4. Feature spec source: `E:\Scripts\Markdown Viewer\CLAUDE.md`.

## Conventions

- Comments in English. Fix root causes, not symptoms.
- Each feature = one commit; commit messages end with a `Co-Authored-By` trailer.
- Don't commit `bin/`, `obj/` (covered by `.gitignore`).
- A feature isn't done without a test on its logic (Core unit or Headless UI).
