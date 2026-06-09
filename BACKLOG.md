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

## M8 — Tabs ergonomics (what M7.5 doesn't cover) · effort M · **core DONE**

| # | Item | Notes |
|---|---|---|
| 11 ✅ | Reuse existing tab when reopening the same file | DONE (`c58afc3`) — `OpenPathAsync` activates the open tab; pure `Core/Services/FilePathEquality`. |
| 18 ✅ | Tab drag-reorder | DONE (`7982335`) — live reorder via a pointer gesture + `MoveTab`; restores selection (the ListBox drops it on `Move`). OS-DnD / arrow-key nav not ported. |
| 25 ✅ | Tab context menu | DONE (`26bd884`) — close / others / right / all via a `ContextFlyout`; commands on the shell VM, reached through a tab `Shell` back-ref. |
| 30 ✅ | Tab tooltip with full path | DONE (`26bd884`) — `ToolTip.Tip` = `FilePath`. |
| 17 ✅ | Copy file path / copy file name | DONE (`e103206`) — new `IClipboardService` port. |
| 27 ✅ | "Reveal in explorer" via `IShellService` port | DONE (`0611b9e`) — cross-platform `Process.Start` (explorer /select · open -R · xdg-open). |
| 28 | Prominent open-error notification (InfoBar) | Status-bar-only is easy to miss. |
| 24 | Button tooltips | |
| 23 | Tab open/close animation | |
| 26 | Editor context menu (copy / select all) | |
| 18b | Multi-file open dialog (`AllowMultiple`) | |
| — | Tab "changed on disk" dirty dot | Pairs with M14 live-reload. |

## M9 — In-document search (find) · effort L · ported ★high-value · **find DONE**

**Find DONE** (source): Ctrl+F find bar (a chrome strip in `DocumentView`, NOT an overlay), highlight all
(amber wash via an `IBackgroundRenderer`, current match outlined in the accent), next/prev (Enter/Shift+Enter),
case + regex toggles (invalid regex reddens the `.*` toggle), an N/M counter; pure `Core/Text/TextSearch`;
a markdown tab opens in Source to search; "Найти…" palette entry. **Replace → M15** (editing + save —
replacing in-memory text you can't save is premature, like undo/redo). **Preview text-highlight deferred**
(Markdown.Avalonia 11 exposes no search/highlight API; the rendered tree is opaque and Av11 has no
`TextHighlighter` — a research item).

## M10 — Sync-scroll, active-heading, TOC polish · effort M · ported + polish

| Item | Notes |
|---|---|
| Sync-scroll source ↔ preview | **Heading-anchor based** (original abandoned percentage — drifted on long docs). |
| Active-heading highlight in the outline | Markdown.Avalonia `HeaderScrolled`. |
| TOC nav: scroll heading to top | M4 `BringIntoView` lands at the edge. |
| Breadcrumbs (markdown + code) | Original had heading breadcrumbs + a minimap. |
| Wiki-links `[[name]]` | Small preprocessor add. |
| `_underscore_` emphasis | Renderer gap; risky text transform — evaluate. |

## M11 — Math rendering (LaTeX) · effort L · ported

`$$…$$` / `\[…\]` / `\(…\)` (single `$` intentionally NOT a delimiter). Investigate
**CSharpMath.Avalonia** (native, no WebView) vs alternatives. Re-verify the library before committing.

## M12 — Diagrams (Mermaid / PlantUML / charts) · effort XL · ported · ⚠ risk

Hard without WebView (Mermaid is JS; PlantUML needs a server/jar — and **leaks source to an external
service, must stay opt-in/off-by-default** as in the original). Chart.js blocks too. Research native
options or a bundled renderer; may stay deferred. Don't start before M9 lands.

## M13 — Export (HTML / PDF / print) · effort L–XL · ported

Self-contained HTML export first (achievable). PDF without WebView is non-trivial (render the visual
tree or a PDF lib; original rasterized → non-selectable text). Also: copy-as-rich-text, native Print.
Port `IExporter` + `Platform/`.

## M14 — Live-reload (file watcher) · effort M · ported

`IDocumentWatcher` (FileSystemWatcher in `Platform/`) → reload on external change, preserving scroll;
mark inactive changed tabs with a dirty dot instead of auto-rerender. Pairs with M6 session work.

## M15 — In-place editing + save · effort L–XL · ported · ⚠ scope decision

Turns the viewer into a light editor (the original had this): edit mode over the preview, **Ctrl+S save
via file write**, paste-image-as-data-URI, spellcheck, edit FAB. **Big scope call — "viewer" vs "editor".**
Decide before starting; could stay deferred if SeriousView remains read-only.

---

## Ported feature pool (from the original viewer — slot opportunistically, not milestones)

Code-view **decorations** (`cv-*`: timestamps · uuid · ip/mac · email · hashes · `file:line:col` paths ·
TODO/FIXME · log levels · units · dates, with hover tooltips) · **JSON pretty-print** toggle ·
**CSV/TSV as table** (sortable, sticky header) · **code outline** (breadcrumbs + minimap, per-language
symbol regex) · **text-file outline + section folding** · **smart typography** (display-only) ·
**indent guides** · code-view line numbers · **stats panel** (word/char/sentence + Russian readability) ·
selection word count · **HTML-fragment preview** (Alt+H) · whole-file HTML render toggle ·
**settings import/export** · image lightbox/zoom · YAML front-matter panel · reading presets ·
**multiple themes** (original had a `DARK_THEMES` set) · **help modal** · drop-overlay polish.

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
Av12). Done since: **M7.5** chrome (6 contextual toolbar · 8 Settings▸Layout), **M9** find, and **M8 core**
(reuse-tab #11, context menu #25, tooltip #30, copy path/name #17, reveal #27, drag-reorder #18). Next:
remaining **M8** polish (#28 open-error InfoBar, #24 button tooltips, #23 tab animation, #26 editor context
menu) or **M10** sync-scroll / active-heading.
