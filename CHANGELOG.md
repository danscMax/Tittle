# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added — M15 "In-place editing"

- Edit in the source view; **Ctrl+S** writes the file back (UTF-8); unsaved-changes ●
  marker on the tab; the saved tab refreshes in place with the reading position kept.
- Checkbox click-to-toggle: clicking a ☐/☑ glyph in the preview flips the task in the file
  (fence-guarded; refuses while there are unsaved edits).

### Added — M13 "Export"

- Self-contained themed HTML export (Markdig; wiki links become relative `name.md` hrefs).
- Copy-as-rich-text (CF_HTML on Windows, `text/html` elsewhere, markdown plain fallback).
- Print / save-as-PDF via the default browser (light-theme HTML hand-off).

### Added — full feature port from the original viewer

- Code view: cv-* token decorations with hover tooltips (timestamps, UUIDs, MAC/IP, emails,
  hashes, `file:line:col`, TODO, log levels, HTML entities, units, dates), indent guides,
  code minimap, per-language symbol outlines + plain-text outlines in the TOC, section
  folding for text files, CSV/TSV as a sortable sticky-header table, JSON pretty-print,
  smart typography for `.txt`/`.log`.
- Preview: copy buttons on code blocks, click-to-sort tables, collapsible heading sections,
  image lightbox, YAML front-matter panel, emoji `:name:`, back-to-top button.
- Navigation: breadcrumbs for code/text outlines, TOC unread marks + ☆ heading bookmarks
  (persisted per file), scroll-% in the status bar, reading-width presets.
- Shell: document statistics window (RU-adapted Flesch) + selection word count, settings
  import/export, F1 help window, Midnight and Ocean dark theme variants.

### Added — M9–M12, M14 (viewer milestones)

- In-document find (Ctrl+F): highlight-all, next/prev, case/regex, match counter.
- Position sync on the preview↔source toggle; active-heading scroll-spy; TOC lands
  headings at the viewport top; wiki-links (`[[name]]`); conservative `_underscore_` italics.
- Block math `$$…$$` / `\[…\]` rendered natively (CSharpMath — no WebView, no JS).
- Live-reload: the active tab refreshes on external changes (reading position survives);
  inactive tabs get a "changed on disk" dot with manual reload.

### Added — M5–M8 (foundation)

- Robust file ingestion: encoding detection (BOM/UTF-8/Windows-1251), binary detection,
  size limits, friendly error overlays.
- Persistence: atomic `settings.json` (theme, window placement, session restore).
- Keyboard: Ctrl+O/W/Tab, font zoom, line numbers, word wrap, go-to-line.
- Shell: ☰ menu, omnibar, Ctrl+K command palette, resizable TOC sidebar, tabs ergonomics
  (drag-reorder, context menu, multi-open, reveal in explorer), single-instance forwarding,
  open-error InfoBar.

### Changed — M2.1 "Premium top bar"

- Unified VS Code-style title bar: the brand, document tabs, theme/open buttons and the
  system min/max/close buttons now share a single strip painted in the app colour
  (FluentAvalonia `AppWindow` + `ExtendsContentIntoTitleBar`).
- Flat, compact document tabs (close ✕ on hover, accent underline on the active tab),
  replacing the oversized default tab headers.
- The welcome screen is shown on startup; the demo document moved to an "Open sample"
  button on it.
- Premium frosted-glass welcome: AcrylicBlur backdrop plus soft accent glows for depth.

### Fixed

- The editor surface now follows the app theme at runtime (Light+/Dark+); it previously
  stayed on the theme it was first installed with (dark editor on the light theme).

### Added — M2 "Visual/UX foundation"

- FluentAvaloniaUI (Fluent v2 theme + system accent).
- Premium Dark/Light palettes (gradient chrome, accent status bar, design tokens).
- Theme modes: Dark / Light / Auto (follow OS); editor syntax theme follows the app
  theme (Light+/Dark+).
- Mica/Acrylic window backdrop on Windows 11 with a solid fallback elsewhere.
- Animations: smooth chrome recolor on theme change, fade-in.
- Recent files (MRU, persisted under %AppData%) + a premium welcome screen.
- Drag-and-drop files into the window to open them.
- Empty-state welcome view when no tabs are open.

### Added — M1 "Skeleton"

- MVVM + DI application skeleton (CommunityToolkit.Mvvm,
  Microsoft.Extensions.DependencyInjection).
- Open files via a native dialog (Avalonia 11 StorageProvider) and from a
  command-line argument.
- Tabbed documents (`DocumentTabViewModel` + `TabControl`).
- Syntax highlighting via an AvaloniaEdit TextMate attached behavior, with
  per-editor lifetime management.
- Instant Light/Dark theme switch through `DynamicResource` design tokens.
- Status bar bound to the active tab.
- Central Package Management; three-project layout
  (`SeriousView` / `SeriousView.Core` / `SeriousView.Tests`).
- Cross-platform CI (build + test on Windows/Linux/macOS).
- Unit + Avalonia.Headless tests for file open, tabs, and theme switching.

### Changed

- Rolled the foundation back from Avalonia 12.0.4 to 11.3.17 for a mature,
  fully-stocked viewer ecosystem (see README for the rationale).
