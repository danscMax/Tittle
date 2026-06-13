# SeriousView — Backlog (single source of "what's next")

One ordered list. Combines **three inputs**:
1. **The 40 improvements** (visual / functional / UX / reliability audit). Items 1–8, 10 done.
2. **Features ported from the original** HTML/WebView viewer (`E:\Scripts\Markdown Viewer` —
   its `CLAUDE.md` is the feature spec; the viewer is large, so its features fan out across M9–M16 + the pool).
3. **The M7.5 shell redesign** — the chrome/layout direction chosen after 100 mockups
   (design artifacts in `plans/shell-redesign/`, gitignored; decision recorded in project memory).

Roadmap-driven: one milestone at a time, each feature = its own commit (Core-first pure logic,
thin UI, a test per feature, visual QA). Ordering principle: **crash/correctness foundations →
shell/UX foundation → everyday ergonomics → major viewer features → heavy rendering extras →
optional editing**. `★` = audit priority. Effort: S / M / L / XL.

> Order is a recommendation, not a contract — any milestone can be pulled forward (e.g. in-document
> Search M9 is very valuable and could go earlier; its find-bar can reuse M7.5's command-palette seam).

---

## ✅ Done

- **M1** skeleton · **M2 / M2.1** visual+UX (premium chrome, themes, AppWindow, tabs, recent, welcome)
- **Visual quick-wins 1–8** (+ press effect #10): tab hover anim, file-type icons, accent button,
  title-bar shadow, slim scrollbars, recent hover, welcome fade-in, segmented status bar
- **Structure refactor** (feature-slices, `ARCHITECTURE.md`)
- **M3** markdown rendering (preview/source toggle, GFM, code highlight, admonitions, task lists, footnotes, hardened links)
- **M4** TOC/outline sidebar (heading parse, source + in-place preview navigation)
- **M5** robust file ingestion (items 31–38): encoding/BOM (UTF-8/16/32 → Windows-1251), binary
  detection, CR/CRLF→LF, size limits (no highlight >5 MB, don't load >50 MB), guarded async startup,
  friendly errors, notice overlay, encoding·EOL in the status bar
- **M6** persistence (items 21★ 22★ 39 40 + session): typed `AppSettings` in one atomic `settings.json`;
  theme restored at startup; window size/position/maximized restored (off-screen re-centring +
  title-bar chrome-offset compensation); session reopens last tabs (arg > session > welcome); crash log
- **M7** keyboard & editor controls (items 12 13 14 15 16 19 20 29): tunnelling KeyDown dispatcher
  (Ctrl+O/W, Ctrl+Tab, Ctrl+±/0 + wheel zoom, Ctrl+L, Alt+Z, Ctrl+G); shared `EditorOptions`;
  caret position in the status bar; auto-focus editor; go-to-line input in the status bar
- **Dev tooling**: portable single-file build (`build.ps1/.bat` → `dist/SeriousView.exe`), dev run
  (`run.ps1/.bat`), per-user file association (`install/uninstall-fileassoc.ps1/.bat`), DPI-aware QA shots
- **M7.5 shell (partial) + M8/shell ergonomics + tech-debt**: ☰ menu; status-bar preview/source toggle
  (eye/`{}`) + wrap/numbers/zoom cluster; **resizable + persisted outline sidebar** (`GridSplitter` →
  `LayoutSettings.OutlineWidth`); sidebar-panel icon; draggable title-bar; content padding; recent temp/dead
  pruning; **single-instance** file-forward (hardened). **Tab content kept alive** (`ItemsControl` + `IsActive`,
  no re-template on switch). Tech-debt audit: pipe ACL + race fixes, debounced zoom writes, cached TextMate
  `RegistryOptions`, virtualized outline, dead-code removal. *(M7.5 4 Omnibar ✅ · 5 Ctrl+K palette ✅ `653ef20`;
  6 contextual toolbar · 8 Settings▸Layout still open.)*
- **Post-full-port UX polish (2026-06-11, after the M15/full-port docs commit `b0b25a8`)** — not new
  milestones, chrome refinement on top of the completed port: **dedup audit #1–#5** (`233a045`,
  consolidated duplicate implementations) · **chrome icons as vector `PathIcon`** (`8facc7e`, kills the
  Symbols-font `.notdef` race) · **editable omnibar address field** (`38d7e23`, type-a-path to open) ·
  **single continuous reading glow** across the body (`e65c86b`) · **«Поддержать автора» donate window**
  (`c5d688f` — new feature, NOT from the original viewer: pure `Core/Support/DonationDirectory` +
  `Features/Donate/DonateWindow`, reached from the ☰ menu) · two shell fixes (`b5d356a` caption-button
  column reserve, `fc8a5ae` tab-title width clamp + thin tab-strip scrollbar).

---

## ★ M7.5 — Shell redesign & customization · effort L · **DONE**

**Design locked** (100 mockups → synthesis → `combos`/`tools`/`custom` in `plans/shell-redesign/`).
**Default layout = menu hidden behind ☰**; the whole chrome is driven by settings, not hard-coded.
Visual language: **Geist** font, **Windows Fluent/Mica**, NATIVE Windows caption (—  ▢  ✕) — no macOS chrome.
Etalon title row: `brand · ☰ menu · omnibar (path · 📂 · ⌘) · native caps`. Zoom lives ONLY in the status bar.

**Phases (each = a commit):**

| # | Phase | Notes |
|---|---|---|
| 1 ✅ | Core `AppSettings.Layout` | `MenuPlacement{Bar,TitleBar,Hidden}` (default Hidden), `ToolbarMode{Off,Contextual,Fixed}`, `ViewTogglePlacement{Tabs,StatusBar,Omnibar}`, `ShowOmnibar`, `ShowRail`. **Modernize (audit 2026-06-08): `JsonSerializerContext` source-gen + `"schemaVersion"` field → versioned migrations** (no silent data loss as fields grow each milestone; also AOT-friendly). Pure Core + test. **Done `e21931b` (+ `LayoutOptions` seam `d2ee4d9`).** |
| 2 ◐ | Chrome render by Layout | Rewrite `MainWindow.axaml` into conditional sections that read `Layout`. **Started: ☰ visibility binds `MenuPlacement==Hidden` via `EnumToBoolConverter` — first real consumer of `Layout`.** |
| 3 ✅ | ☰ menu + dropdown | Hamburger default; classic menu-bar + in-title-bar are presets. Sections grow (Файл·Правка·Поиск·Вид·Инструменты·Тема·Справка). **Done (this commit): ☰ replaces the wordmark; standard `MenuFlyout`/`MenuItem` (FA-themed) with Файл (Открыть/Пример/Недавние ▸/Закрыть) and Вид (Тема radio · Перенос/Номера checks · Перейти к строке); shortcut hints via `InputGesture`. Bar/TitleBar presets deferred to phase 8.** |
| 4 ✅ | Omnibar | File path + 📂 Open + ⌘ palette entry, toggled by `ShowOmnibar`. **Done: centred inset field in the caption row (path · 📂 · ⌘); ☰ + tab strip reflowed below the caption; ⌘ shares the Ctrl+K palette seam.** |
| 5 ✅ | **Command palette Ctrl+K** | Action hub (Open, Theme, View, Outline, Search, Export, Settings…). **Done `653ef20`: top-level `CommandPaletteWindow` (NOT Popup/OverlayLayer — overlay over AvaloniaEdit won't repaint) + `FuzzyMatcher` (fzf-lite: `opfil`→`Open File`), with tests.** |
| 6 ✅ | Contextual toolbar | Thin icon row under the tabs, driven by `ToolbarMode` (Off/Contextual/Fixed). **Done `83f2ef3`: Find + wrap + numbers; wrap/numbers relocated from the status bar with a fallback when Off (pure `ToolbarVisibilityConverter` keeps them in one place); zoom stays in the status bar. Replace/undo/redo/indent → M15 (editing).** |
| 7 ◐ | View toggle + theme access | Предпросмотр/Исходник segmented toggle by the tabs; **Theme moves into the ☰ menu + palette** (no standalone button). Keep Light/Dark/Auto. **Done: theme is now ☰ Вид ▸ Тема (radio Тёмная/Светлая/Авто), standalone Тема button removed; Предпросмотр/Исходник already by the tabs. Palette entry pending phase 5.** |
| 8 ✅ | Settings → Раскладка panel | **Done `42404ab`: ☰ ▸ Раскладка (+ palette) opens a top-level window bound to the shared `LayoutOptions` — ShowOmnibar/ReadingMode/ToolbarMode toggle the chrome live and persist; two-way `EnumRadioConverter`. Not-yet-built knobs (MenuPlacement Bar/TitleBar, ViewTogglePlacement, ShowRail) omitted until their chrome exists.** |

**Fixes folded into M7.5 (found during the visual audit):**

| Fix | What |
|---|---|
| go-to-line overlap ✅ | Status text vs go-to-line input overlapped on welcome — guarded on `HasTabs` (`b37f4ba`). |
| Recent files ✅ | name + folder display (tooltip = full path); **temp/dead-path pruning done** — pure `RecentFilePathPolicy` + `File.Exists` filter prune missing/`%Temp%` entries on load and never record them. |
| Content padding ✅ | preview/source padded + readable column; decorative reading-mode background toggle. |
| Draggable title-bar ✅ | empty title-strip zone now drags the window (hit-test-invisible chrome fill). |
| **Single-instance (#11b) ✅** | file-open forwards to the running window as a new tab (per-user Mutex + named pipe, fail-open). **`CurrentUserOnly`-hardened + concurrency/race fixes** in the tech-debt audit (`c9bb766`). Also fixes the settings.json save race. |
| Window icon (#9) / brand | Drop the redundant brand text; add a real window/app icon (needs a brand asset). **Still open.** |

---

## M8 — Tabs ergonomics (what M7.5 doesn't cover) · effort M · **DONE** (dirty dot → M14)

| # | Item | Notes |
|---|---|---|
| 11 ✅ | Reuse existing tab when reopening the same file | DONE (`c58afc3`) — `OpenPathAsync` activates the open tab; pure `Core/Services/FilePathEquality`. |
| 18 ✅ | Tab drag-reorder | DONE (`7982335`) — live reorder via a pointer gesture + `MoveTab`; restores selection (the ListBox drops it on `Move`). OS-DnD / arrow-key nav not ported. |
| 25 ✅ | Tab context menu | DONE (`26bd884`) — close / others / right / all via a `ContextFlyout`; commands on the shell VM, reached through a tab `Shell` back-ref. |
| 30 ✅ | Tab tooltip with full path | DONE (`26bd884`) — `ToolTip.Tip` = `FilePath`. |
| 17 ✅ | Copy file path / copy file name | DONE (`e103206`) — new `IClipboardService` port. |
| 27 ✅ | "Reveal in explorer" via `IShellService` port | DONE (`0611b9e`) — cross-platform `Process.Start` (explorer /select · open -R · xdg-open). |
| 28 ✅ | Prominent open-error notification (InfoBar) | DONE (`642eeef`) — FluentAvalonia `InfoBar` (Error) above the content; 7 s auto-dismiss (a newer error supersedes the timer) + ✕ via a two-way `IsOpen`; session restore aggregates skipped files into one summary instead of silence. |
| 24 ✅ | Button tooltips | DONE (`84267cc`) — the tab ✕ was the last control without a tip; menu items keep none by design (headers are the label). |
| 23 ✅ | Tab open animation | DONE (`526cbd5`) — 180 ms entrance fade via `ContainerPrepared`, skipped while drag-reordering (Move() recreates containers); opacity-only (no RenderTransform animator on Av11); close stays instant by design. |
| 26 ✅ | Editor context menu (copy / select all) | DONE (`54de229`) — `ContextFlyout` on the source editor: Копировать (disabled w/o selection) · Выделить всё · Найти…; click handlers (flyout content gets no DataContext until shown) + `InternalsVisibleTo` for the headless enabled-state test. |
| 18b ✅ | Multi-file open dialog (`AllowMultiple`) | DONE (`c082e33`) — `PickFilesAsync` returns every picked local path; `OpenFileAsync` funnels each through `OpenPathAsync` (pipe/args already handled lists). |
| — ✅ | Tab "changed on disk" dirty dot | DONE with M14 (`5959cec`) — accent dot on external change; manual reload for inactive tabs. |

## M9 — In-document search (find) · effort L · ported ★high-value · **find DONE**

**Find DONE** (source): Ctrl+F find bar (a chrome strip in `DocumentView`, NOT an overlay), highlight all
(amber wash via an `IBackgroundRenderer`, current match outlined in the accent), next/prev (Enter/Shift+Enter),
case + regex toggles (invalid regex reddens the `.*` toggle), an N/M counter; pure `Core/Text/TextSearch`;
a markdown tab opens in Source to search; "Найти…" palette entry. **Replace → M15** (editing + save —
replacing in-memory text you can't save is premature, like undo/redo). **Preview text-highlight deferred**
(Markdown.Avalonia 11 exposes no search/highlight API; the rendered tree is opaque and Av11 has no
`TextHighlighter` — a research item).

## M10 — Sync-scroll, active-heading, TOC polish · effort M · **DONE**

| Item | Notes |
|---|---|
| Sync-scroll ✅ | DONE (`59e10b7`) — **position sync on the preview↔source TOGGLE** (no split-view in this app): viewport-top anchored as (nearest heading + fraction to the next) via pure `Core/Text/HeadingAnchors` (`9a2c416`); sync only scrolls, never moves the caret; Background-priority restore + one-shot LayoutUpdated retry; TOC jumps cancel pending syncs. Zero headings degrades to proportional. |
| Active-heading highlight ✅ | DONE (`bb2abfb` + `14d3f8b`) — `ActiveHeadingOrdinal` written by the view from scroll in BOTH modes (preview: cached content-space heading Ys, invalidated only on extent change; source: first visible line); 3px accent bar in a fixed marker column via `OrdinalMatchConverter`. NB: Markdown.Avalonia `HeaderScrolled` EXISTS but is wired to the inner ScrollViewer we disable — own geometry instead. |
| TOC nav to top ✅ | DONE (`151ee60`) — preview via direct `PreviewScroll.Offset` (BringIntoView parked headings at the BOTTOM edge), source via the editor's template ScrollViewer + `GetVisualTopByDocumentLine` (`ScrollToLine` centers; `TextEditor.ScrollToVerticalOffset` is a silent no-op). Ctrl+G inherits land-at-top. |
| Breadcrumbs ✅ | DONE (`c46ee7c`) — markdown-only ancestor chain (`MarkdownOutline.AncestorChain`) as a chrome strip under the find bar, both view modes, segments navigate. Code breadcrumbs + minimap stay in the ported pool (need per-language symbol outlines). |
| Wiki-links ✅ | DONE (`c2140a8` + `c670e6d`) — `[[name]]` → link when a sibling `name.md` exists (resolver injected into the preprocessor; existence snapshots once per tab until M14), else plain text; click opens via `Shell.OpenPathAsync` through `WikiHyperlinkCommand` (traversal-safe, `wiki:` never reaches Process.Start; http/https/mailto policy untouched). Outline shows raw `[[name]]` (accepted). |
| `_underscore_` ✅ | DONE (`f8e42a4`) — conservative display-only pass `_x_`→`*x*` (word-boundary flanks, no intraword, fences/inline code/link URLs masked); `__x__` deliberately untouched — the renderer renders it as UNDERLINE natively. |
| Split-view live sync ✅ | DONE (`95341eb`…`816ee78`, 4 waves + fix): a side-by-side source↔preview layout with live mutual scrolling. `DocumentViewMode.Split` + code-managed `SplitGrid` in `DocumentView.SplitLayout.cs` (the single Source/Preview subtrees re-hosted, never duplicated); orientation Horizontal/Vertical + ratio persisted in `LayoutSettings` (schema v2). Live mutual scroll via the `HeadingAnchors` capture/apply, loop broken by **value-based echo suppression** (a synchronous bool latch wouldn't cover Avalonia's deferred ScrollChanged) — source is master for the marker/%/anchor. Entry: status-bar `◫` toggle + Ctrl+\ (Key.OemPipe) + palette (toggle + orientation) + Настройки ▸ Раскладка radio. 21 new tests; visually verified in Dark + Light. |

Also fixed here (`f06eba5`): giant fenced code blocks in the preview — embedded AvaloniaEdit editors
can't size themselves under our infinite-height outer-scroll layout (estimates read ~2× the real
line height; the infinite inner viewport clamped every click-scroll to 0). Heights are now pinned
from a measured `VisualLine`; the preview swallows `RequestBringIntoView` (all navigation is
explicit `Offset` writes).

## M11 — Math rendering (LaTeX) · effort L · ported · **block math DONE**

**Block math DONE**: `$$…$$` / `\[…\]` (single `$` intentionally NOT a delimiter) — a pure
preprocessor pass turns blocks into a `::: math` container with the LaTeX **percent-encoded**
(opaque to the container parser); `AdmonitionBlockHandler` decodes and renders a native
**Sylinko.CSharpMath.Avalonia** `MathView` (the maintained CSharpMath fork for Avalonia ≥11.3;
original is dead at 0.5.1). Parse errors render inline (`DisplayErrorInline`) — no crashes on
garbage. Fenced code is masked via `MarkdownCodeRegions`. **Still open**: inline `\(…\)`
(Markdown.Avalonia exposes no inline-extension seam — research item, lives in the pool).

## M12 — Diagrams (Mermaid / PlantUML / charts) · effort XL · ported · ⚠ risk

Hard without WebView (Mermaid is JS; PlantUML needs a server/jar — and **leaks source to an external
service, must stay opt-in/off-by-default** as in the original). Chart.js blocks too. Research native
options or a bundled renderer; may stay deferred. Don't start before M9 lands.

## M13 — Export (HTML / PDF / print) · effort L–XL · ported · **DONE**

**Self-contained HTML export DONE** (`c04dc79` + `40c8815`): pure `Core/Export/HtmlExporter` —
RAW markdown through **Markdig** (advanced extensions; approved dependency) into one portable
file with an inline themed stylesheet; wiki links become relative `name.md` hrefs via the SAME
token regex as the viewer pass; block math stays as authored (no JS in a self-contained file).
Shell command via `IFileDialogService.SaveFileAsync` — ☰ Файл ▸ «Экспорт в HTML…» + palette;
errors → InfoBar. **Copy-as-rich-text DONE** (`a308ee1`): pure `Core/Export/ClipboardHtml`
builds the CF_HTML envelope (byte offsets into the UTF-8 payload, fragment markers inside
&lt;body&gt;); `IClipboardService.SetHtmlAsync` puts "HTML Format" + plain-markdown fallback
on the clipboard (non-Windows: "text/html"). **Print / save-as-PDF DONE** (`b4f0412`): the
LIGHT-theme HTML export goes to a temp file and opens in the default browser
(`IShellService.OpenWithDefaultApp`) — its print dialog covers paper AND selectable-text PDF.
A native rasterized PDF was deliberately NOT built: off-screen preview rendering trips the
embedded-editor geometry, and the browser output is strictly better (selectable text).
Doc-like Word export: rich-text paste into Word covers the use case.

## M14 — Live-reload (file watcher) · effort M · **DONE**

`IDocumentWatcher` port + `Platform/DocumentWatcher` (`5959cec`): one FileSystemWatcher per
DIRECTORY with ref-counted names (catches editor temp-write/rename dances), 300 ms per-path
debounce with last-kind-wins (File.Replace = one Changed; a lone delete = Removed), overflow
over-notifies, fail-open on unwatchable paths. The shell mirrors watches onto file-backed tabs via
the CollectionChanged funnel. **Active tab auto-reloads** on change (`c837150`): a FRESH tab VM
swaps in place (DocumentText is immutable — the swap refreshes preview/outline/search caches and
the wiki existence snapshot for free), ViewMode survives, selection restored (the MoveTab lesson),
IOException retries once, failures keep content + InfoBar. **Inactive tabs get the dirty dot** and
reload MANUALLY (user decision): tab context menu + palette (`e239b62`). **Reading position
survives reload** (`142efc6`): scroll-spy writes the M10 heading anchor per scroll; the fresh view
consumes a one-shot RestoreAnchor after first layout. Removed/renamed files keep their tab and
last-loaded content (dot + one InfoBar). This also closes the M8 leftover (the dirty-dot row).

## M15 — In-place editing + save · effort L–XL · ported · **DONE** (approved 2026-06-11)

**Core DONE** (`3c04a9e`): the source editor was already editable — the missing piece was
persistence. `DocumentText` stays the loaded truth; the live buffer is reached through a pull
seam (`EditorTextProvider`, wired by the view) and an `IsEdited` flag (length-first compare —
keystrokes are O(1), programmatic reloads stay clean) drives a ● unsaved marker on the tab.
**Ctrl+S** (dispatcher + ☰ Файл ▸ Сохранить + palette) writes UTF-8; a file-backed tab then
refreshes through the M14 watcher reload (fresh VM, caches + reading position for free); the
sample asks for a target and opens the saved file. **Checkbox click-to-toggle DONE**
(`d0c843d`): a click on the ☐/☑ glyph zone in the preview flips the N-th task box in the RAW
file via pure `Core/Text/TaskListToggle` (same `TaskItem` regex + the `MarkdownCodeRegions`
fence guard as the glyph pass, so preview order = raw order); guarded against unsaved edits.
**Deferred with reasons**: paste-image-as-data-URI — Avalonia 11's `IClipboard` has no
portable image read (platform formats; revisit on the Av12 DataTransfer API); spellcheck —
AvaloniaEdit has none built in and there is no OS hook outside WebView/TextBox (a Hunspell
integration would be its own milestone); edit FAB — our preview/source toggle already covers
mode switching.

---

## Ported feature pool (from the original viewer — slot opportunistically, not milestones)

Full-port goal (2026-06-11): carry over ALL functionality from `E:\Scripts\Markdown Viewer`.
Re-audited against its CLAUDE.md — the pool below is the complete gap list (✓-items moved out).

**Ported batch DONE (2026-06-11)** — nine pool features, each its own commit + tests:
**JSON pretty-print** toggle (`37b48fb` — display-only `SourceText` channel, raw `DocumentText`
stays truth) · **code-symbol + plain-text outlines** for non-markdown tabs (`59d5005` —
per-language regex `SymbolOutline` for .cs/.py/.js/… + `TextOutline` for `Глава N`/ALL-CAPS/`====`
headings, feeding the same TOC panel) · **CSV/TSV as a sortable table** (`d7fe211` — RFC4180-light
`DelimitedTable`, sticky header, numeric-aware click-to-sort, 10k-row cap, ▦ status-bar toggle) ·
**emoji `:name:` shortcodes** (`b59093f`) · **smart typography** for .txt/.log (`6cea8bb` —
display-only `--`→— `->`→→ `...`→… "x"→«x», code-line guard) · **stats window + selection word
count** (`5f365e4` — words/chars/sentences/reading-time + Russian-adapted Flesch) · **settings
import/export** (`c82d3bc` — whitelisted `AppSettings` via the source-gen JSON context, live
re-apply on import) · **back-to-top button** in the preview (`7157c61`) · **help window** F1
(`ac02b75` — shortcut reference, Esc closes).

**Ported batch 2 DONE (2026-06-11, visually QA'd)** — eight more, each its own commit + tests:
**cv-* decorations** (`247b42d` — pure `Core/Text/CodeDecorations` composite-regex scanner:
timestamps · uuid · mac/ip · email · 32/40/64-hex hashes · `file:line:col` · TODO/FIXME ·
log levels · HTML entities · units · dates; 2000-char ReDoS guard + regex timeout; themed
`Cv*Brush` palette painted by a `DocumentColorizingTransformer` over TextMate; **hover
tooltips** via AvaloniaEdit `PointerHover` — decoded entity, byte count, «через N дней») ·
**indent guides** (`d0bdfb6` — pure `IndentGuides` tab-stop geometry, blank lines bridge the
block; `IBackgroundRenderer`, code tabs only — prose isn't striped) · **copy button on preview
code blocks** (`7d4d102` — a ghost ⧉ floated over each fenced block: a Grid slipped between
SyntaxHigh's Border and its CodePad, idempotent via the `code-copy-host` class) · **code/text
breadcrumbs** (`52d3b8f` — the M10 strip + outline marker now follow the symbol/text outlines;
one-line scroll-spy gate relaxation) · **scroll-%** in the status bar (`e5f92a3`, both modes) ·
**image lightbox** (`654ce2d` — click a preview image → top-level window, NOT an overlay:
anything floated over AvaloniaEdit won't repaint) · **YAML front-matter panel** (`1ffa5b5` —
preprocessor pass → percent-encoded `::: frontmatter` container → key-value «Метаданные» card;
the HTML export consumes it via Markdig `UseYamlFrontMatter`) · **section folding** for text
files (`4eec2f5` — pure `SectionFolding` + AvaloniaEdit `FoldingManager` margin; fold/unfold-all
in the palette). **URL autolinking in code view**: already built-in — AvaloniaEdit's
`LinkElementGenerator` (https/ftp/www + mailto, Ctrl+Click) is on by default; nothing to port.

**Ported batch 3 DONE (2026-06-11, visually QA'd)** — three more: **click-to-sort preview
tables** (`cb3d008` — Markdown.Avalonia renders GFM tables as `Grid.Table` of Border cells;
a header click reorders `Grid.Row` in place and re-deals the zebra classes; numeric-vs-ordinal
sniffing shared with the CSV view via the new pure `Core/Text/TableSorting`) · **collapsible
heading sections** (`6dcbb21` — click a preview heading to hide its body up to the next
same-or-shallower heading; `IsVisible` only, so the M10 ordinal↔control heading contract and
tops cache survive) · **reading-width presets** (`b649b6d` — `LayoutSettings.ReadingWidth`
Full/Comfort(760)/Narrow(620), radios in Настройки ▸ Раскладка, live + persisted;
`ReadingWidthConverter` drives the preview column's MaxWidth/alignment).

**Ported batch 4 DONE (2026-06-11, visually QA'd)** — three more: **heading bookmarks + TOC
unread marks** (`94c2bb0` — pure `Core/Services/ViewStateStore` over the `ISettingsStore` seam,
one LRU-capped `viewstate.json` (200 files, monotonic touch counter); scroll-spy marks visited
→ the accent dot fades; ☆/★ toggle per TOC row flushes eagerly, visited flushes with the
session; bookmarks surface in the palette as «Закладка: …») · **code minimap** (`1f795c3` —
`MinimapStrip` custom-rendered strip in a SIBLING column beside the editor (overlays over
AvaloniaEdit never repaint): symbol ticks long/short by level + a viewport band; click lands
the line at the viewport top; non-markdown tabs with an outline) · **dark theme set**
(`cd55cf7` — `ThemeMode.Midnight`/`Ocean` as custom `ThemeVariant`s INHERITING Dark, palettes
override only surface tokens; ☰ Вид ▸ Тема radios + palette; cycle walks the dark set first).

**Full theme catalog (2026-06-12/13)** — `1582d60` DeepBlue (Tokyo Night), then `3833f54`
ported the rest of the original viewer's palettes into a **data-driven `Core/Abstractions/ThemeCatalog`**:
14 variants total (Dark, Light + DeepBlue/Midnight/Ocean/Nord/Dracula/SolarizedDark/SolarizedDim/
GruvboxDark/HighContrast on Dark; Sepia/SolarizedLight/GruvboxLight on Light). Each color file in
`Themes/Colors/` overrides only chrome/surface/accent tokens; the markdown preview body deliberately
follows the inherited Light/Dark base (the custom palettes differentiate the CHROME). **Theme smoke
(`max.avalonia-smoke`, 2026-06-13)** rendered all 14 via `tools/HeadlessRender` and caught a
**HighContrast name collision** (`e7129c2`): the variant Key `"HighContrast"` collided with the
platform/FluentAvalonia high-contrast handling → light preview under black chrome; fixed by re-keying
to `"ContrastDark"`. The harness now renders all 14 themes (`818e471`). **Still un-verified: the
custom-palette CHROME** — Layer-1 renders leaf controls only, so the per-theme status bar / tabs /
sidebar / editor surface need a Layer-3 (live-window) pass.

**Markdown extras still open**: checkbox click-to-toggle (write-back guarded by
`fencedCodeRanges` — ships with M15 save).

**Audit 2026-06-11 (3 parallel reviewers over the port wave) — fixed same day**: SEC
script-injection via export/print/clipboard (Markdig now `DisableHtml()`); save clobbering
files with the display transform (unedited save writes `DocumentText`); checkbox index
desync with admonition-nested tasks (nested glyphs excluded on both ends); inconsistent
dark-flag across export paths (one `IsAppEffectivelyDark`, Auto resolves the real variant);
missing try/catch on copy-as-rich-text; settings export leaking the session (preferences
only, import merges); per-keystroke full-document allocation (TextLength-first);
per-rendered-line `DateTime.Now` (cached); TOC multi-binding churn on heading revisit
(`MarkVisited` returns bool); viewstate per-entry cap (`MaxOrdinal`); ru-number table sort.
**Deferred tech-debt (minor, by reviewer priority) — all four resolved 2026-06-13:**
- merge the three preview reflow tree walks (fixup/sorter/collapser) into one pass — **DONE
  `9a1ba52`** (`RunPreviewReflowPasses` does a single bucketed `GetVisualDescendants`).
- cache per-line indent columns (blank-heavy files re-scan ±100 neighbours per frame) — **closed
  by the P9 fix**: `IndentGuideRenderer.EffectiveColumns` memoizes per line (version+tabSize,
  FIFO-cap 4096), so the ±100 scan now runs only on a cache miss (newly-revealed lines). A further
  nearest-non-blank-neighbour memo was rejected as YAGNI (marginal gain over P9).
- `RevealInExplorer` Windows branch → ArgumentList — **kept as a raw `/select,"path"` STRING with
  reason** (`ExpandUnit`/`377a04e`): ArgumentList would quote the whole `/select,path` token and
  break selection on a spaced path; the quote/control-char guard already removes the only injection
  vector and the path is never content-derived. The argument construction was extracted into a pure
  testable `BuildRevealStartInfo(path, RevealPlatform)` seam (all platform branches now unit-tested).
- `ExpandUnit` sign-parse cleanup — **DONE `bb18c0a`** (the sign stays in the parsed slice, applied
  by `double.TryParse`; signed-unit tests added).

**Deferred with reasons**: **HTML-fragment preview / whole-file HTML render** — no HTML
renderer without a WebView (the original leaned on the browser + DOMPurify); revisit if a
native HTML-to-Avalonia control appears. **Drop-overlay polish** — a full-window drag overlay
sits over AvaloniaEdit's GPU surface, which overlays cannot repaint (project memory; the
palette/lightbox use top-level windows instead, but a drag overlay can't be its own window).
Plain drag-and-drop open works and stays.

**Bigger ported milestones still open**: M12 diagrams (Mermaid — JS-only, PlantUML — external
service, MUST stay opt-in/gated like the original's `plantumlAuto:false`; Chart.js blocks),
M13 export beyond HTML (rasterized PDF / print / copy-as-rich-text / doc-like Word), M15
in-place editing + save (Ctrl+S via file write, paste-image-as-data-URI, spellcheck, edit FAB,
editor search; the original deliberately skipped WYSIWYG). Inline math `\(…\)` (M11 leftover).

---

## Traceability — 40 improvements → where

- **Done (1–8, 10):** visual quick-wins + press effect.
- **Done — M5 reliability:** 31★ 32★ 33★ 34 35 36 37 38.
- **Done — M6 persistence:** 21★ 22★ 39 40 (+ session restore).
- **Done — M7 keyboard:** 12 13 14 15 16 19 20 29.
- **M7.5 shell:** #9 window icon ✅ (`5a96163` — flat book+quill, exe `<ApplicationIcon>` + `Window.Icon`), draggable title-bar, recent-files fix, padding, go-to-line fix, single-instance(11b).
- **M8 tabs:** 11 17 18 23 24 25 26 27 28 30.
- Improvements 1–40 are fully placed. Ported-only features (search, sync-scroll, math, diagrams,
  export, live-reload, editing, + the pool) are M9–M16 and the ported pool.

**Status (2026-06-13):** the full-port goal is COMPLETE and the 2026-06-12 tech-debt audit is fully closed
(Waves A–D, 22 findings, `8af0fd0`…`32f385b`). **Split-view live sync is DONE** (`95341eb`…`816ee78`, 828 tests, +21)
and **DocumentView code-behind split is DONE** (`84f8f77`). The **full 14-theme catalog** is in
(`1582d60`/`3833f54`); a `max.avalonia-smoke` theme smoke (2026-06-13) rendered all 14 and fixed a
HighContrast name collision (`e7129c2`). **Theme-catalog chrome pass is DONE** (2026-06-13, `ea85e50`):
Layer-3 live-window shots of all 14 themes (`max.avalonia-smoke` + a local `gui-drive.ps1`) + a deterministic
WCAG audit computed from the AXAML hex. HighContrast = AAA everywhere; cohesion clean (no phantom glyphs /
clipping). Fixed: muted/status text below AA 4.5:1 in SolarizedDark/Dim, GruvboxDark, Sepia, SolarizedLight
(hue-preserving nudge to ≥4.6) + the F-11 catalog-swatch drift for DeepBlue/Ocean/Nord; two guard tests added
(swatch↔AXAML, AA floor for all 14). The light-family swatch convention (GruvboxLight/Sepia/SolarizedLight
mirror `SidebarSurfaceBrush`, not `EditorSurfaceBrush`, since their editor surface ≈ the background) was
**codified + guarded** (`b3d7138`: per-field doc on `ThemeInfo` + `AllSwatches_MatchTheirAxamlSurfaces` over all 14).
**Published 2026-06-13:** public GitHub repo **`danscMax/SeriousView`** (main-only workflow, no feature branches),
bilingual EN/RU README with badges + 4 themed hero screenshots (`docs/screenshots/`), and a **redesigned app
icon** (glossy emerald `</>` on transparent, `52dd193`, via `tools/IconForge`). **Released `v0.1.0` 2026-06-13**
(`9a13668`, `<Version>` in `Directory.Build.props`): GitHub Release with self-contained single-file Windows
binaries (win-x64 + win-arm64, `build_all.ps1`); README has a download badge. **Genuinely open** (pick by priority):
**(a) release CI workflow** — build + attach Linux/macOS (and Windows) binaries automatically on a `v*` tag
(the v0.1.0 release ships Windows only; CI proves Linux/mac build green);
**(b) M12 diagrams** (hard, WebView-less; Mermaid is JS-only, PlantUML leaks source → must stay opt-in).
The four minor tech-debt items are now **CLOSED**
(`9a1ba52` reflow-walk merge · P9 indent-column memo · `377a04e` RevealInExplorer kept-as-string-with-reason
+ `BuildRevealStartInfo` seam · `bb18c0a` ExpandUnit sign-parse), and the **2026-06-13 global audit is DONE**
(11 findings F-01…F-11, 0 critical/high, `7a26bad`…`beed747` — see `AUDIT_REPORT.md`: settings-save logging,
donate-link allowlist, IP-octet validation, watcher dispose-under-gate, crash-log stderr fallback, named shell
event handlers + guarded omnibar, error-bar CTS null, inline-rewrite dedup, DeepBlue/Ocean/Nord surface contrast).
xUnit v3 stays **Av12-gated** (see «Deferred by decision»); the carried-over Lows were
re-verified 2026-06-12 and closed as accepted. **Av12 migration stays blocked** (Markdown.Avalonia alpha-only,
FluentAvalonia not ported — re-verified 2026-06-12; it also gates xUnit v3 via `Avalonia.Headless.XUnit`). The
historical log of completed work follows.

Done since: **M7.5** chrome (6 contextual toolbar · 8 Settings▸Layout), **M9** find, **M8 core**
(reuse-tab #11, context menu #25, tooltip #30, copy path/name #17, reveal #27, drag-reorder #18), and the
**M8 polish** (#28 open-error InfoBar + session-restore summary, #24 ✕ tooltip, #23 tab entrance fade,
#26 editor context menu, #18b multi-file open) — M8 is closed except the "changed on disk" dirty dot,
which shipped with M14 live-reload. Done since: **all of M10** (toggle-position sync via pure
`HeadingAnchors`, active-heading scroll-spy + outline marker, TOC/Ctrl+G land-at-top, markdown
breadcrumbs, wiki-links `[[name]]`, conservative `_underscore_` italics), the giant-fence preview
fix (`f06eba5`), the **preprocessor fence-guard retrofit** (`b50e801` — legacy passes no longer
transform inside ``` fences), **all of M14** (live-reload + dirty dot + position-preserving
reload), **M11 block math** (Sylinko.CSharpMath fork), **M13 HTML export** (Markdig), the
**nine-feature ported batch** (JSON pretty · outlines · CSV table · emoji · typography · stats ·
settings import/export · back-to-top · help), and **ported batch 2** (cv-* decorations · indent
guides · code-block copy buttons · code/text breadcrumbs · scroll-% · image lightbox ·
front-matter panel · section folding — see the pool section), **ported batch 3** (sortable
preview tables · collapsible sections · reading-width presets), and **ported batch 4**
(heading bookmarks + TOC unread marks · code minimap · Midnight/Ocean dark themes; later
extended to the **full 14-theme catalog** `1582d60`/`3833f54` — data-driven `ThemeCatalog`),
**all of M13** (HTML · rich-text · print/PDF via browser), and **all of M15** (in-place
editing + Ctrl+S + ● marker + checkbox click-to-toggle; paste-image/spellcheck deferred with
reasons). **The full-port goal is now COMPLETE except M12 diagrams** (Mermaid is JS-only,
PlantUML leaks source to an external service and must stay gated opt-in — research/deferred)
and inline math `\(…\)` (no inline-extension seam in Markdown.Avalonia — deferred).

---

## Tech-debt backlog — audit 2026-06-12 (Waves A–D ✅ DONE)

A full 6-axis audit (5 analysts → Devil's Advocate → personal verification) ran; **18 findings fixed** across
5 waves (commits `d54c7af`…`656c956`). Full evidence per finding (file:line, code quote, refute notes):
`plans/tech-debt-full/2026-06-12/{security,reliability,performance,quality}-findings.md`,
`advocate-validation.md`, `verified-findings.md`, `report.md`.

**Then all 22 actionable Wave A–D findings below were implemented (2026-06-12, `8af0fd0`…`32f385b`)** — one
finding = one commit + test, build + `dotnet test` green between each (776 → 802 tests). S5 and Q7 were found
already-mitigated and closed with regression tests only. The checklist is kept ticked for traceability; what's
still open for a future session is **«Deferred by decision»**, the carried-over Lows, and the roadmap features
(all below the checklist).

### Wave A — Reliability (do first; small, low-risk, real leaks/loss)
- [x] **R4 (Medium)** `Features/Shell/MainWindowViewModel.cs` · `Dispose` (~L718) + `ShowError` (~L444) /
  `_errorBarCts` (L434) / `AutoDismissErrorBarAsync` (L454). The last live `_errorBarCts` is never cancelled
  in `Dispose()`, so a window closed with an error bar showing leaks a 7 s `Task.Delay` that then pokes
  `IsErrorBarOpen` on a disposed VM. **Fix:** in `Dispose()` add `_errorBarCts?.Cancel(); _errorBarCts?.Dispose();`.
  **Test:** Headless — show an error, `Dispose()`, assert no throw and the dismissal task is cancelled/completed.
- [x] **R5 (Medium)** `App.axaml.cs` · `ShutdownRequested` (L36/L73) + `Features/Shell/MainWindow.axaml.cs` ·
  `SaveOnClose`. VM flush (`FlushEditorSettings`/`FlushViewState`/`Dispose`) runs only from the window's
  `OnClosing`; a programmatic `desktop.Shutdown()` / OS session-end fires `ShutdownRequested` WITHOUT
  `OnClosing`, losing the last zoom/layout/visited change. **Fix:** also flush+dispose the VM from a
  `ShutdownRequested` handler (idempotent — `Dispose` already guards `_disposed`). **Test:** Headless — mutate
  Editor, raise the shutdown path, assert settings were persisted.
- [x] **R6 / Q16 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `EnsureCodeCopyButton` (~L663). The
  injected copy button's handler is `async (_, _) => { … await clipboard.SetTextAsync(…) … }` with no
  try/catch — a clipboard failure becomes an unobserved UI-thread exception (the project's documented
  async-void foot-gun). **Fix:** wrap the await in try/catch (no-op/status on failure); guard the
  `DispatcherTimer.RunOnce` content swap with an is-still-attached check. **Test:** Headless — a failing
  clipboard fake doesn't throw out of the click.
- [x] **R8 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `OnVmPropertyChanged` ViewMode branch (~L254).
  The `ViewMode`-change branch runs `SyncPositionAcrossModes()` (full `GetVisualDescendants` reflow walk)
  with no `IsActive` guard, unlike the IsActive/IsSearchOpen branches — a kept-alive background tab whose
  ViewMode is mutated (palette path) does a whole-tree walk while hidden. **Fix:** gate on
  `_vm?.IsActive == true` (a hidden tab syncs when it next activates). **Test:** Headless — flip ViewMode on a
  non-active tab, assert no reflow side effect (e.g. width-freeze untouched).
- [x] **R13 / Q14 (Low)** `Features/Viewer/DocumentView.axaml.cs` · `OnGoToLineRequested` (L822) &
  `OnSearchUpdated` (L830) posted lambdas. The inner `Dispatcher.UIThread.Post` reads
  `Source.TextArea.Caret` with no `_vm`/generation re-check; closing the tab in that micro-window derefs a
  detached editor. **Fix:** capture `_vm` + bump `_syncGeneration`, re-check both inside the posted lambda
  (mirror the RestoreAnchor pattern already in `OnDataContextChanged`). **Test:** Headless — null `_vm`
  between raise and pump, assert no throw.

### Wave B — Performance (hot paths; measure intent, keep it cheap)
- [x] **H6 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `OnPreviewScrollChanged` (L368) /
  `SchedulePreviewReflow` (L394) / `_previewFrozen` (L50). During a resize the debounce timer is restarted on
  every scroll-changed event even while the preview width is pinned (no reflow is happening). **Fix:**
  `if (e.ExtentDelta.Y != 0 && !_previewFrozen) SchedulePreviewReflow();`. **Test:** Headless — assert no
  reflow scheduled while `PreviewWidthFrozen` (internal seam exists, L522).
- [x] **H7 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `OnSourceScrollChanged` (L717) /
  `RefreshMinimap` (L731) + `Features/Viewer/MinimapStrip.cs` · `Update`/`InvalidateVisual`. Every source-scroll
  frame calls `Minimap.Update`→`InvalidateVisual`. **Fix:** only `InvalidateVisual` when the visible band
  fraction actually moved (diff against the last band), or throttle to ~30 fps via a coalescing timer like the
  preview reflow. (The `ShowMinimap` guard already skips when hidden — keep it.) **Test:** unit on the
  band-changed predicate, or Headless asserting no redraw when the band is unchanged.
- [x] **P4 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `FixupEmbeddedCodeEditors` (L628) /
  `EnsureCodeCopyButton` (L663). Both run on every reflow tick, walking up 3 parents per embedded editor and
  re-pinning height. **Fix:** early-out when the editor height already equals target within the 1px guard;
  mark a "copy-button-done" flag on the editor so `EnsureCodeCopyButton` is O(1) on repeats. **Test:**
  Headless — second reflow pass does no parent walk / re-add.
- [x] **P5 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `TaskGlyphIndexOf` (~L580 region). Walks the
  ENTIRE preview tree (`GetVisualDescendants().OfType<CTextBlock>()`) per checkbox click, with an ancestor
  scan per candidate. **Fix:** cache the document-ordered toggleable-glyph list during the reflow pass (which
  already walks the tree once) and index into it. **Test:** Headless — toggle returns the right index without
  a full re-walk (assert via a counter seam, or just correctness on a many-task doc).
- [x] **P8 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `CvTooltipAt` (L944) +
  `Features/Viewer/CodeDecorationColorizer.cs` · `ScanCached`. The hover tooltip calls
  `CodeDecorations.ScanLine` fresh, bypassing the colorizer's version-memoized cache for the same line.
  **Fix:** expose `ScanCached(version, lineNumber, text)` on the colorizer and route the hover through it.
  **Test:** unit — a warm line hit doesn't re-scan (assert via a scan counter).
- [x] **P9 (Medium)** `Features/Viewer/IndentGuideRenderer.cs` · `_effectiveCache` (L26, cleared L54). Unbounded
  per-line cache for a static (never-edited) document keeps growing while the tab lives. **Fix:** LRU-cap it
  (e.g. last N visible lines) or clear on detach. **Test:** unit — cache size stays bounded after scanning
  many lines at a fixed document version.
- [x] **P10 (Medium)** `Features/Shell/DocumentTabViewModel.cs` · `OnActiveHeadingOrdinalChanged` (L545) /
  `Breadcrumbs` (L542, calls `MarkdownOutline.AncestorChain` → allocates a list). Raises `Breadcrumbs`
  PropertyChanged on every scroll tick that moves the ordinal by 1. **Fix:** only raise `Breadcrumbs` when the
  ancestor chain root actually changes (compare to the previous ordinal's chain). **Test:** unit — a +1 ordinal
  step within the same parent raises no `Breadcrumbs` notify.
- [x] **P12 (Low)** `Core/Text/TextStatistics.cs` · `Compute` (L23/L27). Four+ full-document passes incl.
  `text.ToLowerInvariant()` (whole-doc alloc) for the syllable count. **Fix:** single char loop computing
  chars/no-spaces/syllables together, lower-casing each char inline. One-shot on panel open, so Low. **Test:**
  existing `TextStatisticsTests` must still pass + a perf-shape note.

### Wave C — Quality / correctness
- [x] **Q7 (Medium)** `Features/Viewer/DocumentView.axaml.cs` · `OnSourceTextChanged` (L139). Toggling a
  display transform pushes new `SourceText` into the editor, which can momentarily diverge from the captured
  baseline and false-flag `IsEdited`. (Mitigated but not closed by the V3 read-only editor.) **Fix:** suppress
  `IsEdited` while a programmatic `SourceText` push is in flight (a guard flag set around the transform toggle),
  or recompute the baseline atomically on toggle. **Test:** Headless — toggling pretty-JSON on/off leaves
  `IsEdited == false`.
- [x] **Q8 (Medium)** `Features/Shell/MainWindowViewModel.cs` · the two `OnSelectedTabChanged` partials
  (L1036 two-arg, L1044 one-arg). The one-arg overload hand-writes `OnPropertyChanged(nameof(IsOutlinePaneVisible))`
  that is load-bearing-by-accident (the property depends on `SelectedTab.HasOutline`). **Fix:** collapse into the
  single 2-arg overload and document the `SelectedTab`-derived dependency (or make `IsOutlinePaneVisible` a
  computed-from helper). **Test:** existing outline-pane-visible tests still green after the collapse.
- [x] **Q12 (Medium)** `Core/Text/MarkdownPreprocessor.cs` · wiki-resolver memo (`var known = new Dictionary<…>`
  region). The memo uses an ordinal (case-sensitive) dictionary while the resolver does a case-insensitive
  `File.Exists`, so `[[Note]]` and `[[note]]` each hit the filesystem and can disagree. **Fix:**
  `new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase)`. **Test:** unit — two casings resolve once
  (one resolver call) and agree.
- [x] **Q17 (Medium)** `Features/Shell/DocumentTabViewModel.cs` · `CsvTable` getter (L295). A property getter
  with side effects parses up to 10k rows synchronously on first bind (blocks the UI thread). **Fix:** move the
  parse into the `FromLoad` warm-up (like `Outline`/`PreviewMarkdown`, now off-thread via `BuildTabAsync`), so
  the getter is O(1). **Test:** unit — `DerivedCachesWarm`-style assert that `CsvTable` is built after `FromLoad`.
- [x] **Q19 (Low)** `Core/Text/TableSorting.cs` · `NumericKey` (L23). Returns `double.MaxValue` for unparseable
  cells, colliding legit `1.79e308` values with the "sort last" sentinel. **Fix:** sort with a
  `(bool parsed, double value)` tuple key so junk always sinks regardless of magnitude. **Test:** unit — a real
  `double.MaxValue` cell sorts before garbage.
- [x] **Q20 (Low)** `Core/Text/MarkdownPreprocessor.cs` · `AppendMathContainer` / front-matter / admonition
  passes. Each emits blank-line-padded `:::` blocks without coalescing adjacent blanks; many `$$` blocks shift
  downstream heading line numbers unpredictably (source-scroll still correct, but fragile cross-pass coupling).
  **Fix:** add a regression test asserting outline line numbers survive a multi-`$$` document; optionally
  coalesce adjacent blanks. **Test:** the regression test itself.

### Wave D — Security (Low)
- [x] **S5 (Low)** `Features/Shell/MainWindowViewModel.cs` · `DescribeError` (L848) and its call sites. Full
  absolute paths (with the user name) are surfaced into the copyable on-screen error InfoBar. **Fix:** use
  `Path.GetFileName(target)` in user-facing messages; keep full paths only in the (already-bounded) crash log.
  **Test:** Headless — an error message contains the file name but not the full path.
- [x] **S6 (Low)** `Core/Export/ClipboardHtml.cs` · `InsertFragmentMarkers` (~L30) / `BuildCfHtml` (~L47). CF_HTML
  StartFragment/EndFragment offsets are computed by substring on the whole HTML; a document containing the
  literal marker tokens could shift them (malformed, not executable — `DisableHtml` holds). **Fix:** insert
  markers around a wrapper whose body boundaries we control, or assert the body contains no literal marker
  strings before computing offsets. **Test:** unit — a doc containing `<!--StartFragment-->` text still yields a
  well-formed envelope.
- [x] **S7 (Low)** `Features/Shell/MainWindowViewModel.cs` · `ImportSettingsAsync` (read of `paths[0]`). The
  validation guard (`SettingsTransfer.Parse`) is in place, but there is no size cap before `File.ReadAllTextAsync`
  — a multi-GB / deeply-nested JSON could OOM/spin. **Fix:** reject files over ~1 MB before reading (settings are
  < a few KB); the typed-record whitelist already blocks gadget attacks. **Test:** unit/Headless — an oversized
  file is rejected with a friendly message.

### Deferred by decision (audit 2026-06-12) — not "todo", revisit triggers below
- **DocumentView code-behind split (Medium) — DONE 2026-06-12 (`84f8f77`).** The ~1298 LOC
  `DocumentView.axaml.cs` was split into 6 `partial class DocumentView` files by concern — same compiled class,
  so behaviour, all fields and the ~25 internal test-seams + x:Name controls stayed untouched (zero test
  changes, 807 tests green): `DocumentView.axaml.cs` (ctor/lifecycle/VM-bind/caret/`TryBrush`/`Unsubscribe`),
  `.Reflow.cs` (reflow-debounce + resize-freeze + embedded-editor fixup + copy buttons + task-glyph cache),
  `.ScrollSync.cs` (scroll-spy + position-sync + heading-tops + navigation + go-to-line), `.Search.cs` (find
  bar), `.Decorations.cs` (cv-* + palette + hover + minimap + folding), `.Interaction.cs` (pointer/lightbox/
  checkbox + context menu). True helper-class extraction was rejected: with the test-seams bound to the class
  itself it would need a forwarder per seam (more indirection, no real gain). `MainWindowViewModel` was already
  split earlier (`DocumentExportService` + `SettingsTransfer`).
- **xUnit v3 migration (Low) — BLOCKED on Av11, Av12-gated (re-investigated 2026-06-12).** v2 (2.9.3) is
  maintenance-only; v3 (3.2.x) is active — the motive stands, but the migration is **not doable on the Av11
  line**: `Avalonia.Headless.XUnit` 11.3.17 depends on `xunit.core >= 2.4.0` (xUnit **v2**), and the
  `[AvaloniaFact]`/`[AvaloniaTheory]` attributes derive from v2's `FactAttribute` — incompatible with the
  separate `xunit.v3.*` assemblies. `xunit.v3` support was added only to `Avalonia.Headless.XUnit` **12.0.3+**
  (depends on `xunit.v3.extensibility.core >= 3.2.2`), which pulls `Avalonia.Headless >= 12.x` → the whole
  Avalonia runtime to 12; CPM can't mix versions. Issue
  [AvaloniaUI/Avalonia#18356](https://github.com/AvaloniaUI/Avalonia/issues/18356) (a request for a standalone
  `Avalonia.Headless.XUnit.v3`) was resolved by folding v3 into the 12.x package — **no 11.x backport**. Scope
  if forced: the ~560 Core/pure tests migrate trivially (no `IClassFixture`/`ICollectionFixture`/
  `ITestOutputHelper`/`Record.Exception` — only `[Fact]`/`[Theory]`/`[InlineData]`/`Assert.*`, and
  `xunit.runner.visualstudio` 3.1.5 + `Microsoft.NET.Test.Sdk` 18.6.0 already support v3), but **139
  `[AvaloniaFact]`/`[AvaloniaTheory]` tests across 19 files break**. **Gate = the Av12 migration** (same blocker
  family as `DataTransfer`/`ClipboardService` CS0618); once on Av12 the test-side change is ~mechanical.
- **CSharpMath fork monitoring (Low).** `Sylinko.CSharpMath.Avalonia` 11.3.1 — single-maintainer MIT fork
  (~3.6k downloads, "no deep optimization" disclaimer); upstream dormant. Watch the GitHub repo for archival;
  no alternative for Av11 block math.
- **FluentAvaloniaUI 2.4.x EOL line (Low).** 2.5+ is net10-only → 2.4.1 gets no patches on .NET 9 / Av11.
  Revisit with the Av12 migration.
- **`ClipboardService` CS0618 deprecations (Low, pre-existing).** Av11 `DataObject`/`DataFormats.Text`/
  `SetDataObjectAsync` obsolete; the replacement `DataTransfer` API lands on Av12. 3 build warnings, no impact.
  Fix as part of the Av12 migration, not before.

### Carried from the prior audit (2026-06-09) — RESOLVED (re-verified 2026-06-12)
A 3-way re-verification confirmed none of these are real code bugs; closed as accepted-with-reason (the prior
audit had already filed them under "Defer / document"). Verdicts:
- **crash.log paths → accept.** `CrashLog.Format` includes the stack trace (which embeds file paths) *by design* —
  it's the debugging value; disclosure is same-user / local / capped (`CrashLogger.MaxBytes` = 256 KB drops the
  whole log past the cap). Locked by `CrashLogTests.Format_IncludesTimestampSourceTypeMessageAndStack`.
- **FileReader byte-cap TOCTOU → not a bug.** `FileReader.LoadAsync` allocates on the checked size and
  `ReadExactlyAsync` reads exactly that — the cap can't be bypassed (a file grown after the check is read
  truncated, never over-cap).
- **JsonSettingsStore File.Move first-write → accept.** A rename to a non-existent target on NTFS is atomic and
  the temp is fully written first; subsequent saves already use `File.Replace`. Revisit only for a non-NTFS edge.
- **caret-handler unsubscribe → already fixed.** `Caret.PositionChanged -= OnCaretPositionChanged` runs in
  `DocumentView.DetachChildHandlers` (mirror of the ctor `+=`); covered by `DocumentViewLifecycleTests`.
- **session active-tab-not-file-backed edge → accept (benign).** `GetSession` drops a non-file-backed active tab
  (the sample isn't restored anyway) and stores a clamped in-range `ActiveIndex`; restore clamps again. Locked by
  `GetSession_SampleActive_ClampsToInRangeIndex` (`8af0fd0`-era convention).
- **obsolete drag-drop API `#pragma CS0618` → Av12-gated.** The `DataTransfer` replacement is Av12-only — keep the
  pragma until the Av12 migration (same gate as `ClipboardService` CS0618; see the Av11-not-12 note in CLAUDE.md).

### Roadmap features — deferred WITH REASON (not debt; see CLAUDE.md "Known gaps")
M12 diagrams (Mermaid is JS-only; PlantUML leaks source to an external service — must stay gated opt-in),
inline math `\(…\)` (no inline-extension seam in Markdown.Avalonia), HTML preview (no WebView), drag-drop
overlay (overlay-over-AvaloniaEdit repaint), paste-image (Av11 clipboard has no portable image read — revisit
on Av12 `DataTransfer`), spellcheck (nothing built into AvaloniaEdit), native rasterized PDF (browser output is
better — deliberately rejected), preview text-highlight (Markdown.Avalonia has no API — research item).
