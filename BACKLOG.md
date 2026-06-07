# SeriousView — Backlog (optimal implementation order)

Single source for "what's next". Combines three inputs:
1. **The 40 improvements** (visual / functional / UX / reliability audit). Items 1–8 are done.
2. **Features ported from the original** HTML/WebView viewer (`E:\Scripts\Markdown Viewer`).
3. **Deferred bits** noted across M2–M4.

Roadmap-driven: one milestone at a time, each feature = its own commit (Core-first pure logic,
thin UI, test per feature, visual QA). Ordering principle: **crash/correctness foundations →
high-value low-risk wins on existing infra → everyday ergonomics → major viewer features →
heavy rendering extras (high effort/risk)**. `★` = audit priority. Effort: S/M/L.

> The milestone *order* is a recommendation, not a contract — any milestone can be pulled
> forward (e.g. in-document Search M9 is very valuable and could go earlier if desired).

---

## ✅ Done

- **M1** skeleton · **M2 / M2.1** visual+UX (premium chrome, themes, AppWindow, tabs, recent, welcome)
- **Visual quick-wins 1–8**: tab hover anim, file-type icons, accent button, title-bar shadow,
  slim scrollbars, recent hover, welcome fade-in, segmented status bar (+ press effect #10)
- **Structure refactor** (feature-slices, `ARCHITECTURE.md`)
- **M3** markdown rendering (preview/source toggle, GFM, code highlight, admonitions, task lists,
  footnotes, hardened links)
- **M4** TOC/outline sidebar (heading parse, source + in-place preview navigation)
- **M5** robust file ingestion (items 31–38): encoding/BOM (UTF-8/16/32 → Windows-1251),
  binary detection, CR/CRLF→LF, size limits (no highlight >5 MB, don't load >50 MB),
  guarded async startup, friendly errors, notice overlay, encoding·EOL in the status bar
- **M6** persistence (items 21★ 22★ 39 40 + session): typed `AppSettings` (theme + window + session)
  in one atomic `settings.json`; theme restored at startup; window size/position/maximized restored
  with off-screen re-centring + title-bar chrome-offset compensation (no drift); session reopens
  last tabs (arg > session > welcome), no `ISessionStore` port; crash log to `%AppData%/SeriousView`.
  **Deferred: window icon #9** (needs a brand asset — own visual-polish step).

---

## M7 — Keyboard & editor controls · effort M

A keyboard-driven viewer. Introduce a `KeyBindings` seam, then the commands. #16 reuses M4's
scroll-to-line infra.

| # | Item |
|---|---|
| 12 | Ctrl+O open, Ctrl+W close tab |
| 19 | Ctrl+Tab / Ctrl+Shift+Tab tab navigation |
| 16 | Go-to-line (Ctrl+G) — reuses M4 `ScrollToLine` |
| 13 | Word-wrap toggle (Alt+Z) — `TextEditor.WordWrap` |
| 14 | Font zoom (Ctrl + / − / 0, Ctrl+wheel) — size is hard-coded 14 now |
| 20 | Toggle line numbers (Ctrl+L) |
| 15 | Caret position (line:col) in the status bar |
| 29 | Auto-focus the editor on the active tab (scroll/Ctrl+F work immediately) |

## M8 — Tabs & shell ergonomics (UX) · effort M

| # | Item | Notes |
|---|---|---|
| 11 | Reuse existing tab when reopening the same file | Opens twice today. |
| 18 | Tab drag-reorder | Deferred from M2 C9 (now a ListBox). |
| 25 | Tab context menu (close / close others / copy path / reveal) | |
| 30 | Tab tooltip with full path | Header is truncated. |
| 17 | Copy file path / copy file name | |
| 27 | "Reveal in explorer" via `IShellService` port | Core stays UI-free. |
| 28 | Prominent open-error notification (InfoBar) | Status-bar-only is easy to miss. |
| 24 | Button tooltips (theme cycles Dark→Light→Auto, etc.) | |
| 23 | Tab open/close animation | |
| 26 | Editor context menu (copy / select all) | |
| 18b | Multi-file open dialog (`AllowMultiple`) | Drag-drop already multi. |

## M9 — In-document search (find / replace) · effort L · ported

Major ported feature, very high value. Find bar over preview + source, highlight all, next/prev
(Enter/Shift+Enter), case + regex toggles. Core search logic + `Features/Search`.

## M10 — Sync-scroll, active-heading, TOC polish · effort M · ported + polish

| Item | Notes |
|---|---|
| Sync-scroll source ↔ preview | Heading-index based (original used this). |
| Active-heading highlight in the outline | Use Markdown.Avalonia `HeaderScrolled`. |
| TOC nav: scroll heading to top (not just into view) | M4 used `BringIntoView` (lands at edge). |
| Wiki-links `[[name]]` | Small preprocessor add. |
| `_underscore_` emphasis | Renderer gap; risky text transform — evaluate. |

## M11 — Math rendering (LaTeX) · effort L · ported

`$$…$$` / `$…$`. Investigate **CSharpMath.Avalonia** (native, no WebView) vs alternatives.
Port `IMathRenderer` + `Features/Viewer`. Re-verify the library before committing.

## M12 — Diagrams (Mermaid / PlantUML) · effort XL · ported · ⚠ risk

Hard without WebView (Mermaid is JS; PlantUML needs a server/jar). Research native options or a
bundled renderer; may stay deferred. Don't start before M5–M9 land.

## M13 — Export (HTML / PDF / print) · effort L–XL · ported

Self-contained HTML export first (achievable); PDF without WebView is non-trivial (render the
visual tree or via a PDF lib). Port `IExporter` + `Platform/`.

## M14 — Live-reload (file watcher) · effort M · ported

`IDocumentWatcher` (FileSystemWatcher in `Platform/`) → reload on external change, preserving
scroll. Pairs well with M6 session work.

---

## Polish pool (slot in opportunistically, not milestones)

Image lightbox/zoom · YAML front-matter panel · code-view decorations (UUID/date/path highlights) ·
text-file outline + section folding · minimap · smart typography · reading presets ·
CSV/TSV-as-table · JSON pretty-print toggle.

---

## Traceability — 40 improvements → where

- **Done (1–8, 10):** visual quick-wins + press effect.
- **Done — M5 reliability:** 31★ 32★ 33★ 34 35 36 37 38.
- **Done — M6 persistence:** 21★ 22★ 39 40 (+ session restore). **#9 window icon deferred.**
- **M7 keyboard:** 12 13 14 15 16 19 20 29.
- **M8 tabs/UX:** 11 17 18 23 24 25 26 27 28 30.
- Improvements 1–40 are fully placed; ported-only features (search, math, diagrams, export,
  sync-scroll, live-reload) are M9–M14.

**Suggested next:** **M7** (keyboard & editor controls) — Ctrl+O/W, Ctrl+Tab, go-to-line (reuses M4),
font zoom, word-wrap, line numbers, caret position. Everyday ergonomics on top of the now-persistent shell.
