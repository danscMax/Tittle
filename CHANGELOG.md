# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
