# Tittle — project guide for Claude Code

Native cross-platform desktop markdown/code viewer (Avalonia, Skia, no WebView).
Open-source, Apache-2.0. A deliberate rewrite of an HTML/WebView markdown viewer.

## Commands

```bash
dotnet build Tittle.sln -c Debug          # build
dotnet test  Tittle.sln                   # unit + Headless UI tests
dotnet run --project src/Tittle           # run (or: Tittle <file>)
dotnet format Tittle.sln                  # apply formatting (CI verifies)
```

For **visual GUI QA**, use the **`max.avalonia-smoke`** skill (the Avalonia sibling of `max.qt-smoke`).
Its testing pyramid: **Layer 1** — headless render of LEAF controls to PNG in both themes via
`Avalonia.Headless` + Skia (`tools/HeadlessRender`, run `dotnet run --project tools/HeadlessRender`
with an output dir; cheap, the default); **Layer 2** — the `Avalonia.Headless` `[AvaloniaFact]` suite
(logic/wiring); **Layer 3** — driving the real window via the skill's `gui-drive.ps1` (last resort,
only for the **chrome/`MainWindow`**, which can't render headless — the FluentAvalonia Symbols-font
crash). `tools/HeadlessRender` is OUT of the `.sln` (never affects the app build/test); its PNG output
goes to gitignored `plans/`.

Requires the **.NET 9 SDK**. Built and tested on Windows/Linux/macOS (see CI).

## Architecture (hold the line)

- **Three projects.** `Tittle` (UI/WinExe) → references `Tittle.Core`
  (pure `net9.0` library). `Tittle.Tests` (xUnit + Avalonia.Headless).
  **See `ARCHITECTURE.md` for the full map and "where new code goes".**
- **Core has NO Avalonia dependency.** Pure logic + ports only (`Core/Abstractions`,
  `Core/Services`, `Core/Text`) — thin, no DDD layers. UI concerns (file dialog,
  theme, clipboard) are interfaces in `Core/Abstractions`, implemented in `Tittle/Platform`.
- **UI is feature-sliced.** Code lives in `Tittle/Features/<Name>` (Shell,
  Welcome, Viewer, …) with namespace = folder (`Tittle.Features.<Name>`);
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

**Re-verified 2026-06-12 — blocker still holds:** Avalonia 12 shipped stable (12.0.x), but
Markdown.Avalonia on 12 is **alpha only** (`12.0.0-a2`; stable is still `11.0.3`) and FluentAvalonia
has **no Av12 release yet** (Material.Avalonia / Semi.Avalonia did port). Don't start the Av12
migration — and the Av12-gated items (paste-image / `DataTransfer`, `ClipboardService` CS0618,
reduced-motion, FluentAvalonia 2.4.x EOL, **xUnit v3 migration** — `Avalonia.Headless.XUnit` 11.x
depends on `xunit.core` v2; `xunit.v3` support lands only in `Avalonia.Headless.XUnit` 12.0.3+,
which pulls the Av12 runtime — issue #18356, no 11.x backport) stay deferred — until both ship stable
12 builds.

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
the seam). Unhandled exceptions logged to `%AppData%/Tittle/crash.log` (`Platform/CrashLogger`
+ pure `Core/Diagnostics/CrashLog`). Window icon (#9) done; **redesigned 2026-06-19**: a skeuomorphic parchment-scroll mark
(text lines + a warm-orange dot — the "tittle" — tied with a ribbon) on a soft sandy rounded tile with transparent
corners (replaced the glossy emerald `</>` `52dd193`, and the original book+quill before it), wired as the exe
`<ApplicationIcon>` + `Window.Icon`; multi-size PNG-framed `.ico` + 512 `.png` in `Assets/`. The art was
AI-generated (Nano Banana via OpenRouter) then background-cleaned; packed into `tittle.png`/`.ico` by
`tools/IconGen2/pack-icon.ps1`. `tools/IconForge` (the old `</>` generator) is kept for history only.
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
Known gaps (deferred): Mermaid/diagrams. (Math, export and **split-view live sync** are now done —
split: `DocumentViewMode.Split`, code-managed `SplitGrid` in `Features/Viewer/DocumentView.SplitLayout.cs`,
live mutual scroll with value-based echo suppression, orientation+ratio persisted, Ctrl+\ = `Key.OemPipe`.)
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
Dark — palettes override only surface tokens). **Full theme catalog** (`1582d60`, `3833f54`):
all themes from the original viewer ported into a data-driven `Core/Abstractions/ThemeCatalog`
(14 variants — Dark, Light + DeepBlue/Midnight/Ocean/Nord/Dracula/SolarizedDark/SolarizedDim/
GruvboxDark/HighContrast inheriting Dark, Sepia/SolarizedLight/GruvboxLight inheriting Light;
each color file overrides only chrome/surface/accent tokens, the preview body deliberately
follows the inherited Light/Dark base). **NB (`e7129c2`):** a custom variant's Key string must
NOT be `"HighContrast"` — it collides with the platform/FluentAvalonia high-contrast handling and
forces a light base into the Markdown.Avalonia auto-style; the accessibility theme's variant is
keyed `"ContrastDark"`. `tools/HeadlessRender` (`max.avalonia-smoke`) renders all 14 themes — that
all-theme smoke is what caught the collision. **M13 COMPLETE**: copy-as-rich-text (pure
`ClipboardHtml` CF_HTML envelope + `SetHtmlAsync` with a markdown fallback) and print/PDF
via the browser (light HTML to temp + `IShellService.OpenWithDefaultApp`; native raster PDF
deliberately rejected — browser output is selectable). **M15 COMPLETE** (user-approved):
the editor was already editable — added the `EditorTextProvider` pull seam + `IsEdited`
(length-first compare) ● marker, **Ctrl+S** UTF-8 write-back that refreshes through the M14
watcher reload (position survives), and **checkbox click-to-toggle** (☐/☑ glyph-zone click →
pure `TaskListToggle` flips the N-th raw task line — same regex + fence guard as the glyph
pass; refuses over unsaved edits). **THE FULL-PORT GOAL IS COMPLETE**; the
deferred-with-reason list (Av12-gated) — inline math (no Markdown.Avalonia inline seam), HTML preview
(no WebView), drop overlay (overlay repaint), paste-image (Av11 clipboard has no portable
image read; revisit on Av12 DataTransfer), spellcheck (nothing built into AvaloniaEdit).
Feature spec source: `E:\Scripts\Markdown Viewer\CLAUDE.md`; ordered backlog: `BACKLOG.md`.
**M12 diagrams DONE (Kroki, opt-in)**: Mermaid can't render natively (browser-only `<foreignObject>`),
so ```mermaid/```plantuml/```dot/… fences in the preview POST to a **Kroki** server → image. Core
`DiagramTypes` (fence→kroki type + format) + `KrokiClient` (POST `{url}/{type}/{format}`); preprocessor
`ConvertDiagramFences` (gated by `Transform(..., diagramsEnabled)`) → `::: diagram` container (percent-
encoded `type|body`); `AdmonitionBlockHandler` renders it async (Mermaid→PNG `Bitmap`, else SVG via
`SvgImage`; in-memory cache; source+error fallback). **Strictly opt-in** (`DiagramOptions`/`DiagramSettings`,
default OFF, configurable URL incl. self-host — diagram text leaves the machine) via Настройки ▸ Раскладка.
HTML-export of diagrams DONE: when enabled, each fence → `![type](krokiGetUrl)` (browser fetches the
image; GET payload zlib+base64url via `Core/Text/KrokiUrl`); disabled → fence stays code. Fence walk
shared with the preview via `MarkdownPreprocessor.WalkDiagramFences`; preview also gained a disk cache
(`Core/Services/DiagramCacheKey`) + render spinner + click-to-lightbox.
**Beyond the port — additional native formats**: XML/NDJSON pretty-print (the JSON pretty
toggle was generalized to one `IsPrettyPrintable` «формат» action dispatching by type in
`SourceText`; persist-field `EditorOptions.JsonPretty` kept for settings compat); TOML/INI/.env/
.editorconfig → a Ключ/Значение «Метаданные» table reusing the CSV overlay (`KeyValueConfig`
→ `DelimitedTable`); and an **in-app PDF viewer** — `FileReader` routes `.pdf` to
`FileLoadKind.Pdf` before the binary classifier, `Features/Viewer/Pdf/PdfView` renders fit-width
pages (virtualized, lazy, off-UI-thread; PDFium serialized via one global gate; Ctrl+± re-renders),
graceful fallback to «открыть внешне» on a missing native. Dep: **PDFtoImage 4.1.1** (pinned to
the 4.x line — SkiaSharp 2.88.x unifies with Av11's 2.88.9; 5.x needs SkiaSharp 3.x → defer to
Av12). Native pdfium ships per-RID via `bblanchon.PDFium` (win-x64/arm64, linux, osx all covered).
Also **standalone image files** — `FileReader` routes raster (`.png/.jpg/.jpeg/.gif/.bmp/.webp/.ico`)
and `.svg` to `FileLoadKind.Image`; `Features/Viewer/Images/ImageFileView` shows them as one `IImage`
(raster `Bitmap` decoded natively by Skia, SVG via `SvgImage`/`SvgSource`), fit-to-window + Ctrl+±
zoom (reuses the preview's `ScaleTransformConverter`), graceful «открыть внешне» fallback. Dep:
**Avalonia.Svg.Skia 11.2.0.2** — pinned to the SkiaSharp 2.88.x line (11.2.7.1/11.3.0+ jumped to
SkiaSharp 3.x → would clash with Av11's 2.88.9; same trap as PDFtoImage 5.x). NB: a sub-namespace
under `Features/Viewer` must NOT be `Image` — it shadows the Avalonia `Image` type (use `Images`).
**M16 editing toolkit + command-intent backbone DONE (2026-06-17)**: a shared editor command-intent layer
(`Core/Editing/IEditorIntent` + `EditorCommandDispatcher` + the `Core/Abstractions/IEditorActions` port, impl
`Features/Viewer/AvaloniaEditorActions` wired by `DocumentView` beside `EditorTextProvider`) so the planned
Phase-2 macros record/replay with no retrofit. On it: **line operations** (`Core/Text/LineOperations` —
sort/dedup/trim/case/move/duplicate/join; parameterized `ApplyLineOp` command → ☰ Правка + palette + Ctrl+D /
Alt+↑↓), **Find & Replace** (Ctrl+H replace row, regex `$1` via `TextSearch.ReplaceAll`, on the LIVE editor
text — find re-scans it too), **EOL conversion** (`ConvertEolIntent` → `LineEndings.ConvertTo`), and
**save-encoding** choice (per-tab `SaveEncodingName` → `SaveEncoding.GetBytes` + `AtomicFile.WriteAllBytesAsync`).
Column/block editing is AvaloniaEdit-built-in (Alt+drag; keyboard column-select Alt+Shift+arrows also works
out of the box); reinterpret-as-encoding shipped (`cbebca0`). Deferred-with-reason: multi-caret, status-bar-click
conversion. **M17 macros DONE**: record/replay/persist the 3-source intent stream, manager dialog, Ctrl+Shift+1..9
quick-slots + custom per-macro key gestures, security-allowlisted `macros.json`. The macro wiring was **extracted
out of the shell VM into `Features/Macros/MacroController`** (`vm.Macros`, `2026-06-20` `ae9518b`) — coupling back
to the VM is just two closures (resolve the active editor's actions, write the status line). More formats (M18)
still planned — see `BACKLOG.md` and `plans/editor-toolkit-macros/`.

**Preview-fidelity pass DONE (2026-06-18)** — closing the VS-Code gap the user flagged on real docs. (1)
**Bold/italic render fix**: Avalonia 11.3.x has an OPEN regression (#18875) that mis-resolves the weight of
VARIABLE fonts, so `FontWeight=Bold` rendered at normal weight everywhere — and `Avalonia.Fonts.Inter`
(`.WithInterFont()`) ships Inter as a variable font. Replaced it with bundled **STATIC** Inter faces
(`Assets/Fonts/*`, OFL) registered as the `fonts:Inter` collection (`Platform/InterFontCollection.cs` +
shared `WithBundledInterFont()`, used by the app, the test harness — now on Skia — and `tools/HeadlessRender`),
pinned as the default; the preview's runs name it explicitly via a `ctxt|CTextBlock` style (the FontManager
default resolves weight but not style, so inline italic needs the explicit family). Revert to `.WithInterFont()`
once #18875 ships fixed. (2) **Bare-URL autolinks**: `MarkdownPreprocessor.ConvertBareUrlsInPlace` wraps bare
http/https runs in `[url](url)` (angle `<url>` autolinks render literally in Markdown.Avalonia); masks keep
URLs in code / existing links / `[ref]:` defs untouched. (3) **Real TextMate highlighting for preview code
blocks**: SyntaxHigh renders fenced code near-monochrome, so `EditorBehavior.ApplyPreviewGrammar` attaches the
source editor's TextMate to the embedded preview editors (grammar resolved from the fence language SyntaxHigh
stashes in `editor.Tag`, by id/alias; SyntaxHigh's built-in cleared; theme-following via `ReapplyGrammar`) +
**language autodetect** for bare fences (pure `Core/Text/CodeLanguageGuess` → a final
`ConvertBareCodeFencesInPlace` pass writes the guessed lang into the opener). See project memory
`avalonia-113-variable-font-bold` + `preview-code-highlighting-textmate`.
**Preview visual restyle DONE (2026-06-18)**: (a) the code-block copy button no longer overlaps the
SyntaxHigh language badge (they swap on hover); (b) **filled bullets** — `MarkdownPreprocessor.Normalize-
ListMarkersInPlace` rewrites `-`/`+` list markers to `*` so every bullet renders as `•` (Disc) not the
hollow `○`; (c) **nicer table header** — the library's pale grey is tinted via `TableHeaderBgBrush`/
`TableBorderBrush` tokens, applied in code (`PreviewTableSorter`, local resource bindings — the library's
table styles beat an app/UserControl Setter); (d) **configurable text density** — `ReadingDensity` preset
(Плотно/Обычно/Просторно in Настройки ▸ Раскладка) drives `CTextBlock.LineSpacing`, applied in the reflow.
**H1/H2 divider lines DONE** (`ebcb54e`): GitHub-style rule under H1/H2 (a Border inserted into the content
StackPanel after each such heading during the reflow — `CTextBlock` has no border of its own).

**Build performance & size tuning DONE (2026-06-19)** — the distributed `build.ps1`/`build_all.ps1` config,
not app code (the app's own `Tittle.dll` is ~3.4 MB; the weight is the self-contained .NET runtime + Skia/
PDFium + render libs). Three measured levers, shipped as the new defaults: **(1)** R2R on + single-file
compression off (`22d4d58`) → cold start to input-idle ~1470 ms → **~740 ms (2×)**; **(2)** partial trimming
(`TrimMode=partial`, `c187b6c`) → **171 MB → 121 MB (−29%)** with zero feature risk — partial trims only
`IsTrimmable` assemblies (the .NET runtime + Avalonia) and keeps our code + reflection-heavy viewer libs
(Markdown.Avalonia/CSharpMath/FluentAvalonia) whole, so the `x:CompileBindings="False"` reflection bindings
and settings JSON never break (full trim deliberately avoided — +17 MB more for binding rewrites + risk).
Escape hatches: `-NoReadyToRun`, `-NoTrim`, `-Compress`. **Startup profiled to the Avalonia floor**: the
~740 ms is spread across framework init (~292 ms/39%), first Skia frame (~184 ms/24%), MainWindow XAML
inflation (~148 ms/20%) — no cheap hotspot remains. See project memory `tittle-build-size-startup`.

## Conventions

- Comments in English. Fix root causes, not symptoms.
- Each feature = one commit; commit messages end with a `Co-Authored-By` trailer.
- Don't commit `bin/`, `obj/` (covered by `.gitignore`).
- A feature isn't done without a test on its logic (Core unit or Headless UI).
- **Git workflow: commit straight to `main` and push — no feature branches, no PRs.**
  This is a solo repo (`danscMax/Tittle`, GitHub); the global "branch off `main` for
  non-trivial work" rule does NOT apply here. Always work on `main` directly and
  `git push origin main` after committing.
