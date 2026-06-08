# ============================================================================
# SeriousView -- remove the Markdown file association (current user, no admin)
# ============================================================================
# Mirror of install-fileassoc.ps1. Removes only our own HKCU keys; an extension
# key is deleted only when its default still points at our ProgId, so a later
# association set by another app is left alone. Repo files are not touched.
#
#   .\uninstall-fileassoc.ps1
#   .\uninstall-fileassoc.ps1 -Extensions .md,.markdown,.mdown
# ============================================================================

param(
    [string[]]$Extensions = @('.md', '.markdown')
)

chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'SilentlyContinue'

$progId = 'SeriousView.Markdown'

Write-Host ''
Write-Host '  Remove Markdown -> SeriousView association (per-user)' -ForegroundColor Cyan

Remove-Item -Path "HKCU:\SOFTWARE\Classes\$progId" -Recurse -Force
Remove-Item -Path 'HKCU:\SOFTWARE\Classes\Applications\SeriousView.exe' -Recurse -Force

foreach ($ext in $Extensions) {
    $keyExt = "HKCU:\SOFTWARE\Classes\$ext"
    $current = (Get-ItemProperty -Path $keyExt -Name '(Default)' -ErrorAction SilentlyContinue).'(Default)'
    if ($current -eq $progId) { Remove-Item -Path $keyExt -Recurse -Force }
    Remove-Item -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext\UserChoice" -Force
}

# Refresh Explorer's association cache.
$sig = @'
using System;
using System.Runtime.InteropServices;
public static class SeriousViewShellNotifyU {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern void SHChangeNotify(uint id, uint flags, IntPtr a, IntPtr b);
}
'@
try {
    Add-Type -TypeDefinition $sig -ErrorAction Stop
    [SeriousViewShellNotifyU]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
} catch {}

Write-Host '  Done. Repo files are untouched.' -ForegroundColor Green
Write-Host ''
