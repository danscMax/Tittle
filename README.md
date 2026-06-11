# SeriousView

A native, cross-platform desktop **markdown & code viewer** (with light editing) built with
[Avalonia](https://avaloniaui.net/) — rendered through Skia, no WebView.

> Status: **feature-complete v0.x.** Milestones M1–M15 shipped: rendering, TOC, search,
> live-reload, math, export, themes, in-place editing. The full feature port from the
> original HTML/WebView viewer is closed (diagrams excepted — see `BACKLOG.md`).

## Features

**Markdown**: GFM preview (tables · task lists · footnotes · admonitions/alerts · emoji),
block math (native CSharpMath, `$$…$$` / `\[…\]`), YAML front-matter panel, wiki-links
(`[[name]]` opens the sibling note), collapsible sections, click-to-sort tables, copy
buttons on code blocks, image lightbox, checkbox click-to-toggle (writes back to the file).

**Navigation**: TOC sidebar with active-heading scroll-spy, unread marks and ☆ bookmarks;
heading breadcrumbs (markdown *and* code symbols); position-preserving preview↔source
toggle; in-document find (Ctrl+F, regex/case); go-to-line; Ctrl+K command palette;
back-to-top; reading-width presets.

**Code & text files**: TextMate syntax highlighting, cv-* token decorations (timestamps,
UUIDs, IPs, hashes, TODOs, log levels, units, dates — with resolved-value hover tooltips),
indent guides, code minimap, symbol/text outlines, section folding, CSV/TSV as a sortable
table, JSON pretty-print, smart typography for plain text, document statistics (RU-adapted
Flesch).

**Shell**: tabs (drag-reorder, context menu, kept-alive content), single-instance file
forwarding, live-reload with a "changed on disk" dot and position-preserving refresh,
session restore, Dark / Light / Midnight / Ocean / Auto themes, settings import/export,
F1 help.

**Editing (M15)**: edit in the source view, **Ctrl+S** saves (UTF-8); unsaved-changes
marker on the tab.

**Export**: self-contained themed HTML (Markdig), copy-as-rich-text (CF_HTML),
print / save-as-PDF via the browser.

## Tech stack

| Area | Choice |
|------|--------|
| UI | Avalonia 11.3 (FluentAvaloniaUI v2, Mica/Acrylic) |
| Markdown | Markdown.Avalonia (preview) · Markdig (export) |
| Editor | AvaloniaEdit + TextMate (TextMateSharp grammars) |
| Math | Sylinko.CSharpMath.Avalonia (native, no JS) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Tests | xUnit + Avalonia.Headless (680+) |
| Packages | Central Package Management (`Directory.Packages.props`) |

> **Why Avalonia 11, not 12?** The viewer ecosystem we depend on is not yet stable on 12
> (re-verified 2026-06-11: Markdown.Avalonia 12 is alpha, FluentAvaloniaUI 3 is preview).
> Migration is planned once both ship stable 12 builds.

## Project layout

```
src/SeriousView        UI (Avalonia, feature slices, services, themes)
src/SeriousView.Core   pure .NET 9 library (abstractions + logic, no Avalonia)
tests/SeriousView.Tests xUnit + Avalonia.Headless
```

## Build & run

Requires the **.NET 9 SDK**.

```bash
dotnet build SeriousView.sln -c Release
dotnet run --project src/SeriousView            # or: SeriousView <path-to-file>
dotnet test SeriousView.sln                     # unit + Headless UI tests
```

Windows portable build: `build.ps1` / `build.bat` → `dist/SeriousView.exe`;
per-user file association via `install-fileassoc.ps1`.

## Contributing

Issues and PRs welcome. The codebase keeps a strict boundary: `SeriousView.Core`
must not reference Avalonia; UI concerns (dialogs, theming, clipboard) live behind
interfaces in `Core` with implementations in `SeriousView`. See `ARCHITECTURE.md`.

## License

[Apache-2.0](LICENSE).
