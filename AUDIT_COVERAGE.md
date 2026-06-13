# AUDIT_COVERAGE — реестр покрытия

Рендеренная сводка `audit-coverage.jsonl` (одна строка на файл). Источник истины — jsonl.

## Счётчики (по файлам)

| tier | status | файлов |
|------|--------|--------|
| deep | deep-read | 43 |
| cheap | cheap-passed | 93 |
| excluded | excluded | 127 |
| **итого** | **0 pending** | **263** |

`263 = git ls-files` целиком → каждый файл в терминальном статусе.

## Что куда отнесено

- **deep-read (43)** — risk-зоны: security/IPC/process (`ShellService`, `SingleInstanceGate`,
  `SingleInstanceMessage`, `*HyperlinkCommand`, `WikiLink*`, `DonateWindow`), file-IO/persistence/startup
  (`FileReader`, `TextEncodingDetector`, `BinaryContent`, `AtomicFile`, `JsonSettingsStore`, `ViewStateStore`,
  `CrashLog*`, `App`, `Program`, `AppSettingsMigrator`), concurrency/perf (`DocumentWatcher`,
  `DocumentView.Reflow/ScrollSync/SplitLayout`, `EditorBehavior`), text/regex (`MarkdownPreprocessor`,
  `CodeDecorations`, `MarkdownCodeRegions`, `TextSearch`, `MarkdownOutline`, `HeadingAnchors`,
  `DelimitedTable`, `TaskListToggle`), большие VM (`MainWindowViewModel` 1094, `DocumentTabViewModel` 748,
  `MainWindow.axaml.cs` 595), export/decorations (`HtmlExporter`, `ClipboardHtml`, `DocumentExportService`,
  `AdmonitionBlockHandler`, `CodeDecorationColorizer`, `PreviewTableSorter`, `DocumentView.Decorations/Interaction`).
  Прочитаны построчно субагентами (6 партиций, schema-forced), spot-check'и валидированы вручную.
- **cheap-passed (93)** — остальной прод-код: axaml-вьюхи, конвертеры, DTO настроек, мелкие сервисы,
  токены тем. Закрыты детерминированным ground-truth (build / dotnet format / jscpd / структурный обзор) +
  ручной spot-check (`WindowPlacementValidator`, `install-fileassoc.ps1` — чисто).
- **excluded (127)** — `tests/**` (80, прод-валидированы), `tools/HeadlessRender` (вне `.sln`),
  `*.bat` / build·run·install `*.ps1` (dev-тулинг), `ScriptKit.ps1` (vendored, канон в другом репо),
  `Themes/Colors/*.axaml` (данные-палитры), docs/config/assets (`*.md`, `*.csproj`, `*.props`, `*.ico/png/svg`,
  `ci.yml` — прочитан, чисто).

## Ground-truth (Ф1)

- **build** (`dotnet build -c Debug`): 0 ошибок, 3 различных warning CS0618 (Av12-deprecation) — известны/отложены.
- **format** (`dotnet format --verify-no-changes`): exit 2 — 1 whitespace-нит (`CodeDecorations.cs:53-54`,
  многобайтовые символы; влияние на ubuntu-CI не подтверждено) → [F-10].
- **jscpd**: 0.06% дублирования (1 клон 7 строк) → [F-09].

## Product/UX (Ф4, дополнено по наблюдению пользователя)

[F-11] — контраст кода-вью в «синих» темах: `Themes/Colors/{DeepBlue,Ocean,Midnight,…}.axaml` (tier=excluded
как данные-палитры) наследуют cv-* и TextMate Dark+ из `Dark.axaml`, откалиброванные под почти-чёрный фон →
синие токены/подчёркивания сливаются с синей поверхностью. Корень — `EditorBehavior.PickTheme` (deep-read)
выбирает грамматику только по базовому Light/Dark, игнорируя цвет surface.

Сырьё: `audit-artifacts/raw/`. Доказательный тест: `audit-artifacts/tests/d1_ip_regex_proof.ps1`.
