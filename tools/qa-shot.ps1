#Requires -Version 5
# SeriousView QA screenshot harness (Windows dev tool, not shipped).
# Captures the running SeriousView window via CopyFromScreen (reliable for the
# AvaloniaEdit GPU surface, unlike PrintWindow). Moves the window to a corner so the
# whole window (incl. status bar) is on-screen. Optionally clicks a button by
# AutomationId (UI Automation) and/or resizes the window first.
#
# Usage:
#   pwsh -File tools/qa-shot.ps1 -Out shot.png
#   pwsh -File tools/qa-shot.ps1 -Out light.png -ClickId ThemeButton
#   pwsh -File tools/qa-shot.ps1 -Out small.png -Width 820 -Height 560
param(
    [string]$Out = "$env:TEMP\sv_shot.png",
    [string]$ClickId = "",
    [int]$Width = 0,
    [int]$Height = 0
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class SvWin {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$p = Get-Process SeriousView -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Error "SeriousView is not running"; exit 1 }
$h = $p.MainWindowHandle

if ($ClickId) {
    $win = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $ClickId)
    $btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($btn) {
        ($btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
        Write-Host "clicked AutomationId '$ClickId'"
        Start-Sleep -Milliseconds 600
    } else {
        Write-Host "WARN: AutomationId '$ClickId' not found"
    }
}

[SvWin]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
$r0 = New-Object SvWin+RECT; [SvWin]::GetWindowRect($h, [ref]$r0) | Out-Null
$mw = if ($Width  -gt 0) { $Width }  else { $r0.Right - $r0.Left }
$mh = if ($Height -gt 0) { $Height } else { $r0.Bottom - $r0.Top }
# Park the window at a fixed corner so the entire window is captured on-screen.
[SvWin]::MoveWindow($h, 30, 30, $mw, $mh, $true) | Out-Null
Start-Sleep -Milliseconds 350
[SvWin]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 3) | Out-Null   # HWND_TOPMOST, NOMOVE|NOSIZE
[SvWin]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 700

$r = New-Object SvWin+RECT; [SvWin]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $ht)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
[SvWin]::SetWindowPos($h, [IntPtr](-2), 0, 0, 0, 0, 3) | Out-Null   # HWND_NOTOPMOST
Write-Host "saved $Out (${w}x${ht})"
