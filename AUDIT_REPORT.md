# AUDIT_REPORT — SeriousView (глобальный аудит)

**Дата:** 2026-06-13 · **Scope:** весь проект (`git ls-files`) · **Режим:** READ-ONLY на исходники.
Исправления — отдельными сессиями по волнам ниже.

---

## 1. Summary

### Карта проекта

- **Что это:** SeriousView — нативный кросс-платформенный desktop-просмотрщик markdown/кода
  (Avalonia 11.3.17, .NET 9, Skia, без WebView). Apache-2.0. Три проекта: `SeriousView` (UI/WinExe) →
  `SeriousView.Core` (чистая `net9.0`-логика, без Avalonia), `SeriousView.Tests` (xUnit + Avalonia.Headless).
- **Масштаб:** 263 файла в git; ~27 282 LOC прод-кода (cs+axaml без тестов) + ~20 090 LOC тестов.
  197 `.cs`, 33 `.axaml`. CPM (`Directory.Packages.props`), Nullable enable, ImplicitUsings, compiled bindings.
- **Точки входа:** `Program.Main` → `App.OnFrameworkInitializationCompleted` (DI composition root) →
  `MainWindow`/`MainWindowViewModel` (singleton, одно окно).
- **CI:** GitHub Actions, 3 ОС (ubuntu/windows/macos) — build + test (Release) + `dotnet format --verify-no-changes`.
- **Зрелость:** высокая. Фичи M1–M15 закрыты, архитектура соблюдена (feature-slices, чистое Core,
  MVVM+DI, токены тем), 80 тест-файлов. Код **защитно написан**: ReDoS-таймауты, allowlist схем ссылок,
  guard'ы инъекций в `Process.Start`, path-traversal-валидация wiki-ссылок, атомарная запись файлов.

### Definition of Done — статус

| Критерий | Статус |
|----------|--------|
| Реестр: 0 pending | ✅ 263/263 терминальны (43 deep / 93 cheap / 127 excl) |
| 8 измерений покрыты | ✅ security · reliability · performance · correctness · quality · duplication · modernization · **product** |
| Каждая находка провалидирована | ✅ 11/11 (чтение кода; 3 — инструментальный proof: regex-тест, jscpd, linter; F-11 — наблюдение пользователя + сверка hex) |
| Граф конфликтов и волны | ✅ §3 |
| Сигнальный блок | ✅ в конце |

### Статистика находок

- **Всего: 11** — Critical **0** · High **0** · Medium **2** · Low **9**.
- **Отклонено: 5** (агенты переоценили severity / неверно прочли поток — см. §4).
- Главный вывод: **критичных и серьёзных дефектов нет**. Самое заметное для пользователя — F-11
  (контраст кода в «синих» темах). Остальное — улучшения диагностируемости, консистентности и косметики.

### Impact × Effort

| | Effort S | Effort M |
|---|---|---|
| **Medium** | F-01 (silent settings-save) · F-11 (контраст синих тем, данные-only) | — |
| **Low** | F-02 F-03 F-04 F-05 F-07 F-08 F-09 F-10 | F-06 |

Всё — быстрые/средние правки (10×S, 1×M). Нет ни одного дорогого/архитектурного долга.

### Решения по развилкам — LOCKED для автономного цикла

Все развилки сняты с пользователем (2026-06-13) и зафиксированы **inline** у находок строкой
**«РЕШЕНИЕ (locked, автономно)»** — F-01, F-03, F-06, F-07, F-08, F-11. Остальные находки
(F-02, F-04, F-05, F-09, F-10) однозначны: цикл выполняет их **`Рекомендация`** дословно, без выбора.

- **F-11** → путь «затемнить фон» (данные-only, точная таблица hex; `PickTheme`/токены НЕ трогать;
  чинить только DeepBlue/Ocean/Nord; прочие тёмные темы не трогать).
- **F-06, F-07, F-08** → чинить все три (латентные/защитные, но решено внести).
- **F-01** → только лог в crash.log, без UI и без проброса.
- **F-03** → ужесточить regex (без `IPAddress.TryParse`).

Вопросов к пользователю в цикле НЕ осталось — все находки самодостаточны для автоматического внесения.

---

## 2. Находки (по убыванию severity)

### [F-01] Medium · reliability · `JsonSettingsStore.cs:60-72`
`Save<T>()` глотает ВСЕ исключения без следа и **не пробрасывает** (в отличие от `AtomicFile.WriteAllTextAsync`,
который rethrow'ит на :38). Если каталог настроек read-only / диск полон / сериализация падает —
настройки молча никогда не сохраняются, причём даже в `crash.log` записи нет. Асимметрия с `AtomicFile`
показывает, что автор различает «документы пользователя» (surfacing) и «настройки» (best-effort) — но
полное отсутствие диагностики делает класс отказов невидимым.
**Рекомендация:** в `catch` добавить `CrashLogger.Write(ex, "SettingsSave")` (свопнуть на best-effort-лог),
оставив политику «не падать».
**РЕШЕНИЕ (locked, автономно):** только лог в crash.log — в `catch` (после очистки temp) вызвать
`CrashLogger.Write(ex, "SettingsSave")`; НЕ пробрасывать, НЕ показывать UI (политика «не падать / не
беспокоить пользователя» сохраняется). `JsonSettingsStore` (`SeriousView.Platform`) уже в UI-проекте,
`CrashLogger` доступен напрямую. **Effort:** S. **Proof:** чтение кода. **Конфликты:** нет.

### [F-11] Medium · product/UX + accessibility · `Themes/Colors/{DeepBlue,Ocean,Midnight,…}.axaml` + `EditorBehavior.cs:128-141`
**Подтверждено наблюдением пользователя:** в «синих» темах подсветка синтаксиса и подчёркивания
(иногда синие) почти не видны на синем фоне — суммарно сильно падает контраст кода.
**Корневая причина (системная, не точечная):** кастомные тёмно-производные темы по дизайну
(`CLAUDE.md`: «override only chrome/surface/accent») переопределяют только surface/accent, а тело
кода-вью наследуют из `Dark.axaml`, который откалиброван под почти-чёрный `EditorSurfaceBrush = #1B1B22`:
- **cv-* декорации** наследуются как есть — несколько из них синего семейства:
  `CvEmailBrush`/`CvUnitBrush = #58A6FF`, `CvPathBrush = #79C0FF`, `CvUuidBrush = #67E8F9`.
- **accent-подчёркивание ссылок** в коде-вью синее (DeepBlue `AccentBrush = #7AA2F7`).
- **TextMate-грамматика** жёстко = Dark+ (`EditorBehavior.PickTheme` выбирает Light+/Dark+ только по
  базовому варианту Light/Dark, игнорируя цвет поверхности) → синие keyword-токены Dark+ тоже под Dark.

А поверхность под этим — насыщенно-синяя: DeepBlue `EditorSurfaceBrush = #172139`, Ocean `#101B26`,
Midnight аналогично. Синее-на-синем: даже там, где формальный luminance-контраст проходит, родственный
оттенок «мутит» мелкие детали (подчёркивания, тонкие токены) — то самое «практически не видно».
Затронуты ~10 тем, наследующих Dark (DeepBlue/Ocean/Midnight/Nord/Dracula/SolarizedDark/SolarizedDim/
GruvboxDark + ContrastDark). Дефолтные Dark/Light не задеты.
**РЕШЕНИЕ (locked, автономно):** путь (1) — **только данные, никакой логики** (`PickTheme` НЕ трогать,
`Cv*Brush`/`AccentBrush` НЕ трогать). Затемнить+нейтрализовать поверхности ТОЛЬКО трёх реально синих
низкоконтрастных тем; остальные тёмные темы (Midnight/Dracula/SolarizedDark/SolarizedDim/GruvboxDark)
НЕ трогать — их поверхность нейтральна или не синяя, унаследованные токены на них читаются.
Точная таблица замен (before → after), три файла:

| Файл | Ключ | Было | Стало |
|------|------|------|-------|
| `DeepBlue.axaml` | `WindowBackgroundBrush` | `#121A30` | `#10131C` |
| `DeepBlue.axaml` | `EditorSurfaceBrush` | `#172139` | `#14161C` |
| `DeepBlue.axaml` | `SidebarSurfaceBrush` | `#1D294A` | `#1A1E2A` |
| `Ocean.axaml` | `WindowBackgroundBrush` | `#0C141D` | `#0A0D12` |
| `Ocean.axaml` | `EditorSurfaceBrush` | `#101B26` | `#0D1116` |
| `Ocean.axaml` | `SidebarSurfaceBrush` | `#121E28` | `#0F1318` |
| `Nord.axaml` | `WindowBackgroundBrush` | `#2E3440` | `#272B34` |
| `Nord.axaml` | `EditorSurfaceBrush` | `#3B4252` | `#2E3440` |
| `Nord.axaml` | `SidebarSurfaceBrush` | `#434C5E` | `#353C49` |

Менять ТОЛЬКО значение `Color="..."` у перечисленных ключей; прочие токены/chrome/accent оставить как есть.
После правки прогнать `dotnet run --project tools/HeadlessRender <out>` (рендерит все 14 тем) и `dotnet build`/`dotnet test`.
**Effort:** S (данные). **Proof:** наблюдение пользователя + сверка hex. **Конфликты:** нет (3 отдельных файла).

### [F-02] Low · security · `DonateWindow.axaml.cs:88-93`
`OpenUrl` проверяет только `Uri.TryCreate(..., UriKind.Absolute, ...)` и передаёт URI в
`Launcher.LaunchUriAsync` без allowlist схемы — расходится с `SafeHyperlinkCommand`, который требует
`MarkdownLink.IsSafe` (http/https/mailto). **Сегодня безопасно**: единственный источник — захардкоженный
`DonationDirectory` (только https), внешний/пользовательский ввод сюда не доходит. Это hardening/консистентность,
не текущая уязвимость. **Рекомендация:** прогнать `url` через `MarkdownLink.IsSafe` перед запуском.
**Effort:** S. **Proof:** чтение кода. **Конфликты:** нет.

### [F-03] Low · correctness · `CodeDecorations.cs:45`
IP-регекс `(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?!\d)` не валидирует диапазон октетов: `256.1.1.1`,
`999.999.999.999`, `192.168.1.256` подсвечиваются как IP. Влияние косметическое — это декорация
синтаксиса (подкраска), не валидация. **Рекомендация:** заменить `\d{1,3}` на
`(?:25[0-5]|2[0-4]\d|[01]?\d?\d)` либо пост-проверить `IPAddress.TryParse`.
**РЕШЕНИЕ (locked, автономно):** вариант с regex — заменить оба `\d{1,3}` (octet и хвост) на группу
`(?:25[0-5]|2[0-4]\d|[01]?\d?\d)`; без `IPAddress.TryParse` (без второго прохода). После — обновить
доказательный тест/прогнать `d1_ip_regex_proof.ps1` (теперь должен показать `256.*`→matched=False).
**Effort:** S. **Proof:** `audit-artifacts/tests/d1_ip_regex_proof.ps1` (подтверждено эмпирически).
**Конфликты:** F-10 (тот же файл, иные строки — порядок в §3).

### [F-04] Low · reliability · `DocumentWatcher.cs:203`
`DirectoryWatch.Dispose()` вызывает `_fsw.Dispose()` **вне** `_gate`. Callback `FileSystemWatcher`,
гоняющийся с teardown'ом, может после очистки `_pending` создать осиротевший debounce-`Timer` и/или
выдать `Changed` уже после disposal. Окно узкое (shutdown приложения / unwatch последнего файла в каталоге;
на `Unwatch` имя уже удалено из `_files`, поэтому поздний `Touch` выходит рано). Не краш — `Touch` не трогает
`_fsw`, так что `ObjectDisposedException` не возникает (вопреки исходной гипотезе агента). **Рекомендация:**
диспозить `_fsw` под `_gate` + флаг `_disposed`, проверяемый в начале `Touch`. **Effort:** S.
**Proof:** чтение кода. **Конфликты:** нет.

### [F-05] Low · reliability · `CrashLogger.cs:31-34`
Crash-обработчик глотает все ошибки без фолбэка: если `crash.log` неписабелен (диск полон / права),
краш не оставляет вообще никакого следа. Намеренно («diagnostics must never throw»), но дешёвый фолбэк
улучшил бы отлаживаемость. **Рекомендация:** в `catch` — `Console.Error.WriteLine` хотя бы первой ошибки.
**Effort:** S. **Proof:** чтение кода. **Конфликты:** нет (родственно F-01 — общий мотив «тихий отказ диагностики»).

### [F-06] Low · quality · `MainWindow.axaml.cs:354-358`
Подписки на события VM (`StatsRequested`/`HelpRequested`/`DonateRequested` — лямбды, захватывающие `this`;
`LayoutSettingsRequested` → метод) никогда не отписываются. **Сегодня безвредно**: и `MainWindow`, и
`MainWindowViewModel` — singleton одного окна (`App.axaml.cs:121-122`), живут весь срок приложения. Это
латентный footgun, если окно когда-нибудь станет не-singleton/пересоздаваемым. **Рекомендация:** вынести
именованные обработчики, отписываться в `SaveOnClose`/`Dispose`.
**РЕШЕНИЕ (locked, автономно):** чинить. Заменить 4 inline-лямбды (`StatsRequested`/`HelpRequested`/
`DonateRequested` + метод `LayoutSettingsRequested`) на именованные методы-обработчики; в `SaveOnClose()`
отписаться от всех четырёх перед `vm.Dispose()`. **Effort:** M. **Proof:** чтение кода. **Конфликты:** нет.

### [F-07] Low · quality · `MainWindow.axaml.cs:211-229`
`OnOmnibarKeyDown` — `async void` без внешнего try-catch, в отличие от `OnDrop` (:413, оборачивает тело →
`CrashLogger`). Реального пути утечки исключения нет (единственный `await` — `OpenPathAsync`, полностью
guarded на :855-881; синхронные `Trim` не бросают), но защита-в-глубину и единообразие желательны.
**Рекомендация:** обернуть тело в try-catch → `CrashLogger.Write(ex, "Omnibar")`.
**РЕШЕНИЕ (locked, автономно):** чинить — обернуть тело `OnOmnibarKeyDown` в try-catch →
`CrashLogger.Write(ex, "Omnibar")`, по образцу `OnDrop:413`. **Effort:** S.
**Proof:** чтение кода. **Конфликты:** нет.

### [F-08] Low · reliability · `MainWindowViewModel.cs:462-463`
`ShowError`: `_errorBarCts?.Cancel(); _errorBarCts?.Dispose(); _errorBarCts = new(...)` — старый CTS
диспозится до того, как отложенная auto-dismiss-задача отработает свой `catch(TaskCanceledException)`.
Низкое влияние (задача проверяет токен; dispose уже отменённого CTS безопасен). **Рекомендация:** обнулить
`_errorBarCts` между dispose и новым присваиванием, либо тегировать задачу generation-токеном.
**РЕШЕНИЕ (locked, автономно):** чинить — простейший путь: `Cancel(); Dispose(); _errorBarCts = null;`
затем создать и присвоить новый CTS. **Effort:** S. **Proof:** чтение кода. **Конфликты:** нет.

### [F-09] Low · duplication · `MarkdownPreprocessor.cs:196-202 ↔ 214-220`
jscpd: near-клон 7 строк — два прохода по inline-строкам (одинаковый каркас `for + IsFencedLine +
MaxInlineLineLength + Contains(':')`). Единственный клон во всём `src` (общая дупликация 0.06%).
**Рекомендация:** вынести общий per-line helper. **Effort:** S. **Proof:** jscpd. **Конфликты:** нет.

### [F-10] Low · quality/CI · `CodeDecorations.cs:53-54`
`dotnet format --verify-no-changes` падает локально (exit 2, WHITESPACE на col 89) на строках с
многобайтовыми символами (`μs`, `₽`, `€`). Литеральных табов в файле нет, EOL = LF — вероятен miscount
колонок форматтером на Windows. **Влияние на ubuntu-CI не подтверждено** (нужна ручная проверка job
«Format check»). **Рекомендация:** прогнать `dotnet format`; если ubuntu-CI зелёный — это локальный артефакт.
**Effort:** S. **Proof:** linter (неопределённость зафиксирована). **Конфликты:** F-03 (тот же файл, строка 45 vs 53-54).

### Известное/отложенное (не новые находки)

- **CS0618 Av12-deprecation** (`DataObject` / `IClipboard.SetDataObjectAsync` / `DataFormats.Text`,
  3 различных warning) — задокументировано в `CLAUDE.md` как отложенное до миграции на Avalonia 12
  (блокер — экосистема: Markdown.Avalonia/FluentAvalonia не stable на 12). Измерение *modernization*
  этим покрыто: единственный сигнал устаревания известен и сознательно отложен.

---

## 3. Граф конфликтов и волны исправлений

**Пересечения:** только **F-03 ↔ F-10** (оба в `CodeDecorations.cs`, но F-03 правит regex-строку :45,
F-10 — пробелы на :53-54; не перекрываются). Все прочие находки в РАЗНЫХ файлах → независимы.

**Волны (для будущих сессий):**

- **Волна 1 (всё независимо, можно параллельно):** F-01, F-02, F-04, F-05, F-06, F-07, F-08, F-09, F-11.
  Каждая трогает свой файл/набор тем; порядок не важен.
- **Волна 2 (один файл, последовательно):** сначала **F-03** (правка regex-строки :45), затем **F-10**
  (`dotnet format` по всему файлу — поглотит любые пробелы, включая внесённые F-03). Так F-10 идёт последним.

Глобальной зависимости/противоречий (dedup-vs-modernize и т. п.) нет — дешёвая параллельная программа фиксов.

---

## 4. Отклонённые находки (валидированы как ложные/переоценённые)

| # | Находка агента | Severity (агент) | Вердикт | Причина (по чтению кода) |
|---|----------------|------------------|---------|--------------------------|
| R1 | `FileReader.cs:17-22` TOCTOU → `EndOfStreamException` пробрасывается | High | **Отклонено** | `EndOfStreamException : IOException`; вызыватели `RestoreSessionAsync:802` и `OpenPathAsync:872` ловят `IOException` → деградирует в «файл недоступен», не краш. |
| R2 | `AtomicFile.cs:30-31` `File.Replace(...,null)` неатомарен / теряет данные | High | **Отклонено** | На одном томе (гарантировано — temp в том же каталоге) `ReplaceFile`/`rename` атомарны; `null` backup идиоматичен (просто без копии); оригинал никогда не теряется (`if File.Exists` + catch+rethrow). Заявление в комментарии корректно. |
| R3 | `App.axaml.cs:56-74` async-void поймает не всё | Medium | **Отклонено** | Тело уже обёрнуто в `try-catch(Exception)` → `CrashLogger.Write` (:63-72). «Не-Exception» исключений в C# не бросают; рекомендованный guard уже на месте. |
| R4 | `DocumentView.ScrollSync.cs:152-160` гонка echo-suppression | High | **Отклонено** | Все обработчики — на UI-потоке; `OnPreviewScrollChanged` и `CancelPendingSync` не могут чередоваться mid-method (один поток). Отложенные applies уже защищены `_syncGeneration`. Конкурентных call-stack'ов нет. |
| R5 | `EditorBehavior.cs:162-167` grammar-apply в try / старая installation утекает | Medium | **Отклонено** | Агент неверно прочёл поток: `ApplyGrammar:167` — ПОСЛЕ try (catch на :152-158 делает `return`). При throw старое валидное состояние намеренно сохраняется (не утечка — `Teardown` его диспозит). Код корректен и прокомментирован. |
| — | `DocumentView.Reflow.cs:91` (Low) | Low | **Отклонено** | Сам агент пометил «No fix required»: код уже снапшотит дерево (:87-90). |

---

## 5. Покрытие

- **deep-read: 43** · **cheap-passed: 93** · **excluded: 127** · **итого 263 — 0 pending.**
- Подробности и обоснование tier'ов — `AUDIT_COVERAGE.md`. Реестр — `audit-coverage.jsonl`.
- Ground-truth (build/format/jscpd) — `audit-artifacts/raw/`. Доказательства — `audit-artifacts/tests/`.

**Измерения:**
security ✅ · reliability ✅ · performance ✅ (Reflow/Colorizer/decorations — без hot-path-проблем) ·
correctness ✅ · quality ✅ · duplication ✅ (0.06%) · modernization ✅ (Av12-deprecation известна/отложена) ·
product ✅ — **F-11** (контраст кода-вью в «синих» темах, подтверждён наблюдением пользователя). Полный
визуальный GUI-QA всех экранов — отдельный скилл `max.avalonia-smoke`, вне read-only-аудита.
