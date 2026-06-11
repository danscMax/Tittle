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
+ pure `Core/Diagnostics/CrashLog`). Window icon (#9) done (`5a96163`): a flat book+quill icon (blue→teal
on a dark squircle), wired as the exe `<ApplicationIcon>` + `Window.Icon`; `.ico`/`.png`/`.svg` in `Assets/`.
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
Known gaps (deferred): Math/KaTeX, Mermaid/diagrams, export, split-view live sync.
**M7.5 (shell redesign) DONE; M8 (tabs ergonomics) core DONE**: ☰ menu (default `MenuPlacement.Hidden`);
status-bar compact preview/source toggle (eye / `{}`) beside the wrap/numbers/zoom cluster;
**resizable + persisted outline sidebar** (`GridSplitter` → `LayoutSettings.OutlineWidth`, clamped 180–480,
committed once on close); sidebar-panel PathIcon; single-instance file forwarding (Mutex + named pipe,
`CurrentUserOnly`-hardened); draggable title-bar; recent-file temp-path pruning; **Ctrl+K command palette**
(top-level window + fzf-lite fuzzy match, `653ef20`); **omnibar header** (active path · 📂 open · ⌘ palette,
toggled by `Layout.ShowOmnibar`). **Tab content is kept
alive** — the body is an `ItemsControl` over `Tabs` with each tab's `DocumentView` toggled by `IsActive`
(NOT a `ContentControl`), so switching tabs is a visibility flip, not a re-template — don't revert (project
memory). A tech-debt audit also hardened the single-instance gate, debounced editor-zoom settings writes,
cached TextMate `RegistryOptions` per theme, and virtualized the outline list.
Done since: **audit quick-wins + a11y** (background GC, 8 KB-head binary classification,
`AutomationProperties.Name` + keyboard focus visuals; reduced-motion deferred to Av12 — no
`PrefersReduceMotion` API on 11) and **M9 in-document find** (Ctrl+F find bar, highlight-all via an
`IBackgroundRenderer`, next/prev, case/regex, N/M counter, source-only; pure `Core/Text/TextSearch`).
**Replace → M15** (editing + save), **preview text-highlight** a research item (Markdown.Avalonia has no
API). Then **M7.5 chrome COMPLETE** — phase 6 contextual editor toolbar (`ToolbarMode`-driven Find · wrap ·
numbers; pure `ToolbarVisibilityConverter`; wrap/numbers relocate from the status bar with an Off-fallback,
zoom stays) + phase 8 Settings ▸ Раскладка (☰/palette → a window bound to the shared `LayoutOptions`;
ShowOmnibar/ReadingMode/ToolbarMode toggle the chrome live + persist; two-way `EnumRadioConverter`).
Then **M8 tabs ergonomics core DONE**: reuse-tab on reopen (#11, pure `Core/Services/FilePathEquality`,
`OpenPathAsync` activates the open tab); tab context menu (#25 close/others/right/all via a `ContextFlyout`,
commands on the shell VM reached through a tab `Shell` back-ref — a flyout is a popup, so it can't walk the
visual tree up to the shell); full-path tooltip (#30); copy path/name (#17, new `IClipboardService`); reveal
in Explorer (#27, new `IShellService`, cross-platform `Process.Start` — explorer /select · open -R · xdg-open);
tab drag-reorder (#18, live pointer gesture + `MoveTab`, which restores `SelectedTab` — the bound ListBox
drops it on `ObservableCollection.Move`). **M8 polish DONE** (M8 closed; only the "changed on disk" dirty dot
remains, paired with M14): open-error **InfoBar** (#28, FluentAvalonia `InfoBar` Severity=Error above the
content, 7 s auto-dismiss via cancellable `Task.Delay` — a newer error supersedes the timer — plus two-way
`IsOpen` ✕; session restore now aggregates skipped files into one summary message instead of dropping tabs
silently); ✕-tab tooltip (#24); tab entrance fade (#23, 180 ms via `ContainerPrepared`, skipped while
drag-reordering because `Move()` recreates containers; opacity-only — Av11 keyframes can't animate the
composite `RenderTransform`); editor context menu (#26, `ContextFlyout` on the source editor — Копировать
disabled w/o selection · Выделить всё · Найти…; click handlers, NOT bindings: flyout content gets a
DataContext only once shown, which headless can't do — `InternalsVisibleTo` lets tests drive the refresh);
multi-file open (#18b, `PickFilesAsync` + `AllowMultiple`, each path funnels through `OpenPathAsync`).
**M10 DONE** (each its own commit, TDD): pure `Core/Text/HeadingAnchors` (position ↔ nearest-heading
anchor + fraction; zero headings → proportional); **position sync on the preview↔source toggle**
(viewport-top probe, Background-priority restore + one-shot LayoutUpdated retry, TOC jumps cancel
pending syncs, caret never moves on sync); **active-heading scroll-spy in both modes**
(`ActiveHeadingOrdinal` written by `DocumentView` like CaretLine; preview = cached content-space
heading Ys invalidated only on extent change, source = first visible line) driving a 3px accent
marker in the outline (`OrdinalMatchConverter`, fixed marker column) and a **breadcrumbs strip**
(`MarkdownOutline.AncestorChain`, markdown-only, both modes, segments navigate); **TOC/Ctrl+G land
at the viewport top** (direct `PreviewScroll.Offset`; source via the editor's template ScrollViewer —
`ScrollToLine` centers and `TextEditor.ScrollToVerticalOffset` is a silent no-op); **wiki-links**
(`[[name]]` → `wiki:` link when a sibling `name.md` exists — resolver injected into the Core
preprocessor, existence snapshots once per tab until M14; `WikiHyperlinkCommand` opens via
`Shell.OpenPathAsync`, traversal-safe, http/https/mailto fallback untouched); **conservative
`_underscore_` italics** (display-only, word-boundary, fences/inline-code/URLs masked via the new
fence-aware `Core/Text/MarkdownCodeRegions`; `__x__` stays — the renderer underlines it natively).
Plus the **giant-fence preview fix** (`f06eba5`): embedded SyntaxHigh AvaloniaEdit editors can't size
under our infinite-height outer scroll (estimates ~2× real line height; infinite inner viewport
clamped click-scrolls to 0) — heights pinned from a measured `VisualLine`, preview swallows
`RequestBringIntoView` (see project memory). Legacy preprocessor passes are still fence-blind
(pre-existing; `MarkdownCodeRegions` makes the retrofit one guard per pass).
The preprocessor's legacy passes (task lists, footnotes, admonitions) are fence-guarded since
`b50e801` — nothing transforms inside ``` fences. **M14 (live-reload + dirty dot) DONE**:
`IDocumentWatcher` port + per-directory FileSystemWatcher with ref-counted names and debounced
last-kind-wins coalescing (File.Replace = one Changed); the ACTIVE tab auto-reloads on external
change by swapping in a fresh tab VM (immutable DocumentText → all caches and the wiki snapshot
refresh for free; selection restored — the MoveTab lesson; ViewMode and the READING POSITION
survive via the M10 heading anchor handed to the fresh view as a one-shot RestoreAnchor);
inactive tabs get an accent dirty dot and reload manually (tab context menu + palette — user
decision); removed/renamed files keep their tab and content (dot + one InfoBar error).
**M11 block math DONE**: `$$…$$` / `\[…\]` → `::: math` containers (percent-encoded opaque
transport; bodies are protected regions — no pass rewrites raw LaTeX) rendered natively by
**Sylinko.CSharpMath.Avalonia** 11.3.1 (maintained CSharpMath fork; garbage LaTeX → the control's
inline error); theme via a `ChromeForegroundColor` twin (MathView.TextColor is a Color). Single
`$` is NOT a delimiter; inline `\(…\)` deferred. **M13 HTML export DONE**: pure
`Core/Export/HtmlExporter` — raw markdown through **Markdig** advanced extensions into ONE
self-contained themed file; wiki links → relative `name.md` hrefs (same token regex as the
viewer); ☰ Файл ▸ «Экспорт в HTML…» + palette via `IFileDialogService.SaveFileAsync`. PDF/print/
rich-text still open. **Standing goal: port EVERYTHING from `E:\Scripts\Markdown Viewer`** — the
complete gap audit lives in BACKLOG's ported pool. **Ported batch DONE** (nine commits,
`37b48fb`…`ac02b75`): JSON pretty-print toggle (display-only `SourceText` channel — raw
`DocumentText` stays truth), code-symbol + plain-text outlines in the TOC panel, CSV/TSV as a
sortable sticky-header table (▦ status-bar toggle), emoji `:name:`, smart typography for
.txt/.log, stats window (F-stats + Russian Flesch) + selection word count, settings
import/export, back-to-top button, F1 help window. **Ported batch 2 DONE** (eight commits,
`247b42d`…`4eec2f5`, visually QA'd): cv-* token decorations (pure `CodeDecorations` scanner +
colorizer + `PointerHover` tooltips), indent guides (pure geometry + `IBackgroundRenderer`,
code tabs only), copy button on preview code blocks (Grid slipped inside SyntaxHigh's Border,
idempotent), code/text breadcrumbs (scroll-spy gate relaxed to any outline), scroll-% in the
status bar, image lightbox (top-level window — overlays over AvaloniaEdit don't repaint),
YAML front-matter → «Метаданные» panel (percent-encoded `::: frontmatter`; export consumes it
via Markdig UseYamlFrontMatter), section folding for text files (`SectionFolding` +
`FoldingManager`, fold/unfold-all in the palette). URL autolinking in code = AvaloniaEdit
built-in (Ctrl+Click). **Ported batch 3 DONE**: click-to-sort preview tables (Grid.Table
rows re-rowed in place, zebra re-dealt; numeric sniff shared with CSV via `TableSorting`),
collapsible heading sections (IsVisible-only — the M10 heading contract survives),
reading-width presets (Full/Comfort/Narrow in Настройки ▸ Раскладка, persisted).
**Ported batch 4 DONE**: heading bookmarks + TOC unread marks (pure `ViewStateStore` →
LRU-capped `viewstate.json`; scroll-spy marks visited, ☆/★ per TOC row, palette
«Закладка: …»), code minimap (`MinimapStrip` in a sibling column — overlays over
AvaloniaEdit never repaint), Midnight/Ocean themes (custom `ThemeVariant`s inheriting
Dark — palettes override only surface tokens). **M13 COMPLETE**: copy-as-rich-text (pure
`ClipboardHtml` CF_HTML envelope + `SetHtmlAsync` with a markdown fallback) and print/PDF
via the browser (light HTML to temp + `IShellService.OpenWithDefaultApp`; native raster PDF
deliberately rejected — browser output is selectable). **M15 COMPLETE** (user-approved):
the editor was already editable — added the `EditorTextProvider` pull seam + `IsEdited`
(length-first compare) ● marker, **Ctrl+S** UTF-8 write-back that refreshes through the M14
watcher reload (position survives), and **checkbox click-to-toggle** (☐/☑ glyph-zone click →
pure `TaskListToggle` flips the N-th raw task line — same regex + fence guard as the glyph
pass; refuses over unsaved edits). **THE FULL-PORT GOAL IS COMPLETE except**: M12 diagrams
(Mermaid JS-only; PlantUML external service, must stay gated opt-in) and the
deferred-with-reason list — inline math (no Markdown.Avalonia inline seam), HTML preview
(no WebView), drop overlay (overlay repaint), paste-image (Av11 clipboard has no portable
image read; revisit on Av12 DataTransfer), spellcheck (nothing built into AvaloniaEdit).
Feature spec source: `E:\Scripts\Markdown Viewer\CLAUDE.md`; ordered backlog: `BACKLOG.md`.

## Conventions

- Comments in English. Fix root causes, not symptoms.
- Each feature = one commit; commit messages end with a `Co-Authored-By` trailer.
- Don't commit `bin/`, `obj/` (covered by `.gitignore`).
- A feature isn't done without a test on its logic (Core unit or Headless UI).
