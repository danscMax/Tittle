---
name: gui-qa
description: Use to visually QA / drive the SeriousView desktop GUI — launch the real app, send keystrokes & shortcuts, click (by coordinate or AutomationId), take DPI-aware screenshots, and review them against the desktop-visual-audit checklist. Triggers — /gui-qa, прокликай интерфейс, запусти и сними скриншот, проверь UI вживую, прогони визуальный тест, посмотри как выглядит в обеих темах, drive the GUI, screenshot the app, click through the UI, live visual QA.
---

# SeriousView GUI QA — drive the real window

The visual Definition-of-Done for any chrome/layout/theme change. Headless xUnit tests (`dotnet test`)
prove logic; they do **NOT** prove the UI is readable, themed, aligned, or that a keyboard shortcut
actually fires. This skill drives the **real running window** and captures it for inspection.

## Why a real window (not in-process render)

SeriousView is **.NET/Avalonia**. Unlike the Qt `/smoke` skill in `E:\Scripts\Docx и pdf обработка`
(which renders widgets in-process via `widget.grab()` because Qt allows offscreen rendering),
SeriousView's **chrome cannot be rendered headless** — the FluentAvalonia "Symbols" glyph font
crashes on `RunJobs` (project memory `headless-appwindow-fonticon-testing`). So visual QA drives the
real window over **Win32** (launch → keystroke/click → `CopyFromScreen`). The in-process path
(`Avalonia.Headless` + `[AvaloniaFact]`) is already used for the **logic/seam** tests in
`tests/SeriousView.Tests`; this skill is the **visual/interactive** half.

## The driver

All actions go through one script (run with the native `pwsh`/`powershell.exe`, never wrapped in bash):

```
.claude/skills/gui-qa/scripts/gui-drive.ps1 <action> [opts]
```

| Action | What |
|---|---|
| `launch -File <path> [-Theme Dark\|Light\|Midnight\|Ocean\|DeepBlue] [-Build] [-Release]` | kill any running instance (single-instance forwards otherwise), optionally `-Build` and seed the theme into `settings.json` (backed up to `.qabak`), launch the exe on a file, wait for the window |
| `key -Key <name> [-Ctrl] [-Shift] [-Alt]` (or `-Vk 0xNN`) | inject a virtual-key keystroke via `keybd_event` (NOT SendKeys). Names: `A`–`Z`, `0`–`9`, `F1`–`F12`, `Enter` `Esc` `Tab` `Space` `Backslash` `Plus` `Minus` `Up`/`Down`/`Left`/`Right`/`Home`/`End` |
| `click -X <n> -Y <n> [-Right]` | mouse click at **window-relative** coords (DPI-aware) |
| `uia -Find <AutomationId> [-ByName]` | find a control by `AutomationProperties.AutomationId` (or `-ByName`) and Invoke it (falls back to clicking its centre — Avalonia's UIA InvokePattern is partial) |
| `shot -Out <path.png> [-W 1300 -H 860]` | restore + position the window, then `SetProcessDPIAware` + `CopyFromScreen` → PNG (NOT PrintWindow — it misses the GPU/AvaloniaEdit surface) |
| `restore` | restore `settings.json` from the launch backup (undo a `-Theme` seed) |
| `kill` | force-kill the app (does NOT run SaveOnClose, so a seeded settings file survives until `restore`) |

## Flow

1. **Build** if code changed: `gui-drive.ps1 launch -File <doc> -Build`. Output screenshots go under
   `plans/gui-qa/screenshots/` (gitignored).
2. **Launch** on a realistic document (use `plans/ux-audit/rich.md` — it exercises headings, admonitions,
   tables, task lists, code). Optionally seed a theme.
3. **Drive** to the state under test — prefer **keyboard shortcuts** (most robust), then `uia` by
   AutomationId, then coordinate `click` as a last resort. Known shortcuts & ids below.
4. **Screenshot**: `gui-drive.ps1 shot -Out plans/gui-qa/screenshots/<name>.png`.
5. **Review**: Read the PNG and sweep it with the **`desktop-visual-audit`** skill checklist
   (readability/contrast · theme cohesion · empty states · layout · interaction · chrome). Report in
   Russian.
6. **Cover the matrix**: at least **Dark + Light** (relaunch `-Theme Light` — hardcoded one-theme
   colours hide here). Empty (welcome, no tabs) vs populated.
7. **Fix-loop**: trace each issue to its root (token/theme file), fix, rebuild, relaunch, re-shoot,
   verify — don't claim "looks fine" without a fresh shot.
8. **Cleanup**: `gui-drive.ps1 restore` (undo theme seed) then `gui-drive.ps1 kill`. Screenshots stay
   in gitignored `plans/` as evidence; delete if not needed.

## App shortcuts & AutomationIds to drive

Shortcuts (central dispatcher, `MainWindow.axaml.cs`): `Ctrl+K`/`Ctrl+Shift+P` palette · `Ctrl+O` open ·
`Ctrl+W` close · `Ctrl+Tab`/`Ctrl+Shift+Tab` tabs · `Ctrl+±/0` zoom · `Ctrl+L` line numbers · `Alt+Z`
wrap · `Ctrl+G` go-to-line · `Ctrl+F` find · `Ctrl+\` split view · `Ctrl+S` save · `F1` help. (The
palette is the catch-all for theme/layout/export actions when no shortcut exists — open it with
`Ctrl+K`; typing the Cyrillic filter via injected keys is unreliable, so prefer seeding state via
`launch -Theme` or the `uia` ids.) AutomationIds: `ViewModeSwitch` `ViewModePreview` `ViewModeSource`
`ViewModeSplit` `OutlineSplitter` `SplitSplitter` `GoToLineBox`.

## Gotchas (baked into the script — don't re-derive)

- `keybd_event` + VK, **not** `SendKeys` (Avalonia maps SendKeys letters to `Key.None`).
- The `\|` key is `VK_OEM_5` (0xDC) → Avalonia `Key.OemPipe` (NOT `Key.OemBackslash`). `-Key Backslash`
  sends 0xDC.
- `SetProcessDPIAware` + `Graphics.CopyFromScreen`; `PrintWindow` misses Skia/AvaloniaEdit surfaces.
  Status bar can clip at 150% DPI — size the window generously (`-W/-H`).
- Single instance: a second launch forwards to the running window — `launch` kills first.
- A force `kill` skips `SaveOnClose`, so a `-Theme`-seeded `settings.json` is preserved; always
  `restore` afterwards so the user's real theme/layout comes back.
- An overlay over AvaloniaEdit's GPU surface won't repaint — that's why chrome (status bar / top-level
  windows) is used, and why a real-window screenshot (not an offscreen render) is the only faithful capture.

## Related
- `desktop-visual-audit` — the checklist to sweep each screenshot with.
- Memories: `verify-gui-visually`, `screenshots-need-dpi-aware`, `headless-appwindow-fonticon-testing`,
  `fluentavalonia-symbolicon-font-race`, `split-view-gotchas`.
