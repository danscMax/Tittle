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
| Split-view live sync | NEW backlog item (out of M10 by decision): a side-by-side layout with live mutual scrolling. `HeadingAnchors` is the ready seam — capture in one pane → `ToPosition` in the other per scroll tick + a re-entrancy latch. |

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

## M13 — Export (HTML / PDF / print) · effort L–XL · ported · **HTML DONE**

**Self-contained HTML export DONE** (`c04dc79` + `40c8815`): pure `Core/Export/HtmlExporter` —
RAW markdown through **Markdig** (advanced extensions; approved dependency) into one portable
file with an inline themed stylesheet; wiki links become relative `name.md` hrefs via the SAME
token regex as the viewer pass; block math stays as authored (no JS in a self-contained file).
Shell command via `IFileDialogService.SaveFileAsync` — ☰ Файл ▸ «Экспорт в HTML…» + palette;
errors → InfoBar. **Still open**: PDF (original rasterized via html2pdf — non-selectable text;
native alternative needs research), native Print, copy-as-rich-text, doc-like Word export.

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

## M15 — In-place editing + save · effort L–XL · ported · ⚠ scope decision

Turns the viewer into a light editor (the original had this): edit mode over the preview, **Ctrl+S save
via file write**, paste-image-as-data-URI, spellcheck, edit FAB. **Big scope call — "viewer" vs "editor".**
Decide before starting; could stay deferred if SeriousView remains read-only.

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

**Markdown extras**: sortable tables (click-to-sort, like `setupTables`) · collapsible heading
sections (`.collapse-icon`) · bookmarks per heading · TOC unread marks (`md-visited-*`) ·
checkbox click-to-toggle (write-back guarded by `fencedCodeRanges` — needs M15 save).

**Chrome/tools**: code minimap (symbol outline is in — the remaining consumer) · reading
presets · **multiple dark themes** (`DARK_THEMES` set).

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

**Suggested next:** the M7.5 foundations (menu, single-instance, persistence, keyboard, resizable sidebar,
Ctrl+K palette, omnibar) and a tech-debt hardening pass are done. **Chosen direction: audit quick-wins + a11y**
(background GC, 8 KB-head binary classification, accessible names + focus visuals; reduced-motion deferred to
Av12). Done since: **M7.5** chrome (6 contextual toolbar · 8 Settings▸Layout), **M9** find, **M8 core**
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
front-matter panel · section folding — see the pool section). Next: the pool remainder
(sortable preview tables, collapsible sections, bookmarks/unread marks, minimap, reading
presets, dark-theme set), then the big open milestones (M12 diagrams · M13 beyond HTML ·
M15 editing — scope decision pending).
