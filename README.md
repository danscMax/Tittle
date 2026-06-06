# SeriousView

A native, cross-platform desktop **markdown & code viewer** built with
[Avalonia](https://avaloniaui.net/) — rendered through Skia, no WebView.

> Status: **M1 (skeleton) complete.** MVVM + DI foundation, file open, tabs,
> instant Light/Dark theming, syntax highlighting. Markdown rendering arrives in M3.

## Features (so far)

- Native Avalonia 11 UI (Skia rendering), no embedded browser.
- Open files via a native dialog (StorageProvider) or as a command-line argument.
- Multiple documents in tabs.
- Syntax highlighting (TextMate grammars / VS Code "Dark+").
- Instant Light/Dark theme switch via design tokens.

## Tech stack

| Area | Choice |
|------|--------|
| UI | Avalonia 11.3 (Fluent theme, Inter font) |
| Editor | AvaloniaEdit + TextMate (TextMateSharp grammars) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Tests | xUnit + Avalonia.Headless |
| Packages | Central Package Management (`Directory.Packages.props`) |

> **Why Avalonia 11, not 12?** The viewer ecosystem we depend on
> (Markdown.Avalonia, FluentAvaloniaUI) is stable only on the 11.x line as of
> mid-2026; on 12 it is still alpha/preview. Migration to Avalonia 12 is planned
> once that ecosystem stabilises.

## Project layout

```
src/SeriousView        UI (Avalonia, MVVM, services, themes)
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

## Contributing

Issues and PRs welcome. The codebase keeps a strict boundary: `SeriousView.Core`
must not reference Avalonia; UI concerns (dialogs, theming, clipboard) live behind
interfaces in `Core` with implementations in `SeriousView`.

## License

[Apache-2.0](LICENSE).
