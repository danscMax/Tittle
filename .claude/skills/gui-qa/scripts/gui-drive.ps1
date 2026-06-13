<#
.SYNOPSIS
  Drive the REAL running SeriousView window for live visual QA - launch, position, inject
  keystrokes, click (by coordinate or by AutomationId via UI Automation), and DPI-aware screenshot.

  NOTE: ASCII-only on purpose. Windows PowerShell 5.1 reads a .ps1 with no BOM as ANSI, so non-ASCII
  bytes (em-dash, arrows, Cyrillic) corrupt the tokenizer. Keep this file ASCII; prose lives in SKILL.md.

.WHY (Avalonia-specific - do not "simplify" these away)
  SeriousView is .NET/Avalonia. Unlike a Qt app, its chrome CANNOT be rendered in-process headless
  (the FluentAvalonia "Symbols" glyph font crashes on RunJobs), so visual QA must drive the REAL
  window via Win32. Baked-in gotchas:
    * keybd_event with VK codes - NOT SendKeys (Avalonia maps SendKeys letters to Key.None).
    * The "\|" key is VK_OEM_5 (0xDC) -> Avalonia Key.OemPipe (NOT Key.OemBackslash).
    * Screenshot via SetProcessDPIAware + Graphics.CopyFromScreen - NOT PrintWindow (misses the
      GPU/AvaloniaEdit surface).
    * Single instance: a second launch FORWARDS to the running window (Mutex+pipe), so kill first.
    * Seeding a theme/layout writes %AppData%\SeriousView\settings.json; a FORCE kill does NOT run
      SaveOnClose, so the seeded file survives - back it up (launch -Theme) and 'restore' it after.

.USAGE
  pwsh -File gui-drive.ps1 launch  -File "E:\path\doc.md" [-Theme Dark] [-Build] [-Release]
  pwsh -File gui-drive.ps1 key     -Key K -Ctrl            # keys: A-Z 0-9 F1-F12 Enter Esc Tab Space Backslash Plus Minus Up Down Left Right  (or -Vk 0xNN)
  pwsh -File gui-drive.ps1 click   -X 120 -Y 40 [-Right]   # window-relative coords
  pwsh -File gui-drive.ps1 uia     -Find ViewModeSplit [-ByName]
  pwsh -File gui-drive.ps1 shot    -Out "E:\...\shot.png" [-W 1300 -H 860]
  pwsh -File gui-drive.ps1 restore
  pwsh -File gui-drive.ps1 kill
#>
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('launch', 'key', 'click', 'uia', 'shot', 'restore', 'kill')]
    [string]$Action,
    [string]$File,
    [string]$Theme,
    [switch]$Build,
    [switch]$Release,
    [string]$Key,
    [string]$Vk,
    [switch]$Ctrl,
    [switch]$Shift,
    [switch]$Alt,
    [int]$X,
    [int]$Y,
    [switch]$Right,
    [string]$Find,
    [switch]$ByName,
    [string]$Out,
    [int]$W = 1300,
    [int]$H = 860
)

chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

# scripts -> gui-qa -> skills -> .claude -> SmartEdit
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$Settings = Join-Path $env:APPDATA 'SeriousView\settings.json'
$Backup = "$Settings.qabak"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class GuiDrive {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int t, bool r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[GuiDrive]::SetProcessDPIAware() | Out-Null

function Get-SvProc {
    Get-Process SeriousView -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
}
function Get-Handle {
    $p = Get-SvProc
    if (-not $p) { throw "SeriousView is not running (launch first)" }
    [GuiDrive]::ShowWindow($p.MainWindowHandle, 9) | Out-Null  # SW_RESTORE (un-maximize so MoveWindow takes)
    return $p.MainWindowHandle
}
function Focus-Window($hwnd) {
    [GuiDrive]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 400
}

# Friendly key name -> Windows virtual-key code.
function Resolve-Vk($name) {
    if ($Vk) { return [Convert]::ToInt32($Vk, 16) }
    if ($name.Length -eq 1 -and $name -match '[A-Za-z0-9]') { return [int][char]($name.ToUpper()) }
    switch ($name) {
        'Enter' { 0x0D } 'Return' { 0x0D } 'Esc' { 0x1B } 'Escape' { 0x1B } 'Tab' { 0x09 }
        'Space' { 0x20 } 'Backslash' { 0xDC } 'Pipe' { 0xDC } 'Plus' { 0xBB } 'Minus' { 0xBD }
        'Up' { 0x26 } 'Down' { 0x28 } 'Left' { 0x25 } 'Right' { 0x27 } 'Home' { 0x24 } 'End' { 0x23 }
        'F1' { 0x70 } 'F2' { 0x71 } 'F3' { 0x72 } 'F4' { 0x73 } 'F5' { 0x74 } 'F6' { 0x75 }
        'F7' { 0x76 } 'F8' { 0x77 } 'F9' { 0x78 } 'F10' { 0x79 } 'F11' { 0x7A } 'F12' { 0x7B }
        default { throw "Unknown key '$name' (use -Vk 0xNN for raw virtual-key codes)" }
    }
}

switch ($Action) {

    'kill' {
        Stop-Process -Name SeriousView -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 400
        Write-Host "killed"
    }

    'restore' {
        if (Test-Path $Backup) { Move-Item -Force $Backup $Settings; Write-Host "restored settings.json from backup" }
        else { Write-Host "no backup to restore" }
    }

    'launch' {
        if ($Build) {
            Write-Host "building..." -ForegroundColor Cyan
            $cfgB = if ($Release) { 'Release' } else { 'Debug' }
            dotnet build (Join-Path $Root 'SeriousView.sln') -c $cfgB --nologo -v q
        }
        Stop-Process -Name SeriousView -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        if ($Theme) {
            if (-not (Test-Path $Backup) -and (Test-Path $Settings)) { Copy-Item $Settings $Backup }
            if (Test-Path $Settings) {
                $json = Get-Content -Raw -LiteralPath $Settings | ConvertFrom-Json
                $json.Theme = $Theme
                ($json | ConvertTo-Json -Compress -Depth 10) | Set-Content -NoNewline -LiteralPath $Settings
                Write-Host "seeded Theme=$Theme (backup at .qabak)"
            }
        }
        $cfg = if ($Release) { 'Release' } else { 'Debug' }
        $exe = Join-Path $Root "src\SeriousView\bin\$cfg\net9.0\SeriousView.exe"
        if (-not (Test-Path $exe)) { throw "exe not found at $exe - run with -Build first" }
        if ($File) { Start-Process $exe -ArgumentList "`"$File`"" } else { Start-Process $exe }
        Start-Sleep -Seconds 4
        $p = Get-SvProc
        if ($p) { Write-Host "launched (pid $($p.Id))" } else { Write-Warning "process not detected yet" }
    }

    'key' {
        if (-not $Key -and -not $Vk) { throw "key requires -Key NAME or -Vk 0xNN" }
        $hwnd = Get-Handle; Focus-Window $hwnd
        $vk = Resolve-Vk $Key
        # NB: PowerShell variables are case-INSENSITIVE, so these must NOT be $CTRL/$SHIFT/$ALT —
        # those would alias the [switch] params $Ctrl/$Shift/$Alt and assigning an int to a switch throws.
        $vkCtrl = 0x11; $vkShift = 0x10; $vkAlt = 0x12; $UP = 0x02
        if ($Ctrl) { [GuiDrive]::keybd_event($vkCtrl, 0, 0, [UIntPtr]::Zero) }
        if ($Shift) { [GuiDrive]::keybd_event($vkShift, 0, 0, [UIntPtr]::Zero) }
        if ($Alt) { [GuiDrive]::keybd_event($vkAlt, 0, 0, [UIntPtr]::Zero) }
        [GuiDrive]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 50
        [GuiDrive]::keybd_event($vk, 0, $UP, [UIntPtr]::Zero)
        if ($Alt) { [GuiDrive]::keybd_event($vkAlt, 0, $UP, [UIntPtr]::Zero) }
        if ($Shift) { [GuiDrive]::keybd_event($vkShift, 0, $UP, [UIntPtr]::Zero) }
        if ($Ctrl) { [GuiDrive]::keybd_event($vkCtrl, 0, $UP, [UIntPtr]::Zero) }
        Start-Sleep -Milliseconds 700
        $mods = "$(if($Ctrl){'Ctrl+'})$(if($Shift){'Shift+'})$(if($Alt){'Alt+'})"
        Write-Host "sent $mods$Key"
    }

    'click' {
        $hwnd = Get-Handle; Focus-Window $hwnd
        $r = New-Object GuiDrive+RECT; [GuiDrive]::GetWindowRect($hwnd, [ref]$r) | Out-Null
        $sx = $r.Left + $X; $sy = $r.Top + $Y
        [GuiDrive]::SetCursorPos($sx, $sy) | Out-Null
        Start-Sleep -Milliseconds 150
        if ($Right) { $down = 0x08; $up = 0x10 } else { $down = 0x02; $up = 0x04 }  # R/L button down/up
        [GuiDrive]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 40
        [GuiDrive]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 600
        Write-Host "clicked window+($X,$Y) -> screen($sx,$sy)"
    }

    'uia' {
        if (-not $Find) { throw "uia requires -Find AutomationId (or -ByName)" }
        Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
        $hwnd = Get-Handle; Focus-Window $hwnd
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
        $prop = if ($ByName) { [System.Windows.Automation.AutomationElement]::NameProperty }
        else { [System.Windows.Automation.AutomationElement]::AutomationIdProperty }
        $cond = New-Object System.Windows.Automation.PropertyCondition($prop, $Find)
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if (-not $el) { throw "UIA element not found: $Find" }
        $invoke = $null
        if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
            $invoke.Invoke()
            Write-Host "invoked $Find"
        }
        else {
            $rect = $el.Current.BoundingRectangle
            $cx = [int]($rect.X + $rect.Width / 2); $cy = [int]($rect.Y + $rect.Height / 2)
            [GuiDrive]::SetCursorPos($cx, $cy) | Out-Null
            Start-Sleep -Milliseconds 150
            [GuiDrive]::mouse_event(0x02, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 40
            [GuiDrive]::mouse_event(0x04, 0, 0, 0, [UIntPtr]::Zero)
            Write-Host "clicked center of $Find ($cx,$cy)"
        }
        Start-Sleep -Milliseconds 600
    }

    'shot' {
        if (-not $Out) { throw "shot requires -Out path.png" }
        Add-Type -AssemblyName System.Drawing
        $hwnd = Get-Handle
        [GuiDrive]::MoveWindow($hwnd, 0, 0, $W, $H, $true) | Out-Null
        Focus-Window $hwnd
        Start-Sleep -Milliseconds 900
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Out) | Out-Null
        $bmp = New-Object System.Drawing.Bitmap ([int]$W), ([int]$H)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size($W, $H)))
        $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
        Write-Host "saved $Out (${W}x${H})"
    }
}
