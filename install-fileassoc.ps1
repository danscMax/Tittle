# ============================================================================
# SeriousView -- associate Markdown files with the app (current user, no admin)
# ============================================================================
# Registers a per-user (HKCU) file association so double-clicking a .md opens it
# in SeriousView. The open command points STRAIGHT at the native exe -- the app
# takes the file path as its first argument, so no .bat/.ps1 shim is needed
# (unlike the old HTML viewer, which had to inject into a template + launch a
# browser). Re-run this after moving the repo or switching -Target.
# Mirror: uninstall-fileassoc.ps1.
#
#   .\install-fileassoc.ps1                          # -> Debug build (dev version)
#   .\install-fileassoc.ps1 -Target Portable         # -> dist\SeriousView.exe
#   .\install-fileassoc.ps1 -Extensions .md,.markdown,.mdown
# ============================================================================

param(
    [ValidateSet('Debug', 'Portable')] [string]$Target = 'Debug',
    [string[]]$Extensions = @('.md', '.markdown')
)

chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$exe  = if ($Target -eq 'Portable') {
    Join-Path $root 'dist\SeriousView.exe'
} else {
    Join-Path $root 'src\SeriousView\bin\Debug\net9.0\SeriousView.exe'
}

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "  ERROR: target exe not found:" -ForegroundColor Red
    Write-Host "  $exe" -ForegroundColor Red
    if ($Target -eq 'Portable') {
        Write-Host "  Build it first:  .\build.bat" -ForegroundColor Yellow
    } else {
        Write-Host "  Build it first:  dotnet build  (or run .\run.bat once)" -ForegroundColor Yellow
    }
    exit 1
}

$progId = 'SeriousView.Markdown'
$cmd    = "`"$exe`" `"%1`""   # native exe + the clicked file as argv[0]

Write-Host ''
Write-Host "  Associate Markdown -> SeriousView  ($Target)" -ForegroundColor Cyan
Write-Host "  exe:  $exe" -ForegroundColor DarkGray
Write-Host "  ext:  $($Extensions -join ', ')" -ForegroundColor DarkGray
Write-Host ''

# 1. ProgId: friendly name + icon (from the exe) + open command.
$keyType = "HKCU:\SOFTWARE\Classes\$progId"
New-Item -Path $keyType -Force | Out-Null
Set-ItemProperty -Path $keyType -Name '(Default)' -Value 'Markdown Document'
New-Item -Path "$keyType\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$keyType\DefaultIcon" -Name '(Default)' -Value "$exe,0"
New-Item -Path "$keyType\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$keyType\shell\open\command" -Name '(Default)' -Value $cmd

# 2. Point each extension's default at our ProgId.
foreach ($ext in $Extensions) {
    $keyExt = "HKCU:\SOFTWARE\Classes\$ext"
    New-Item -Path $keyExt -Force | Out-Null
    Set-ItemProperty -Path $keyExt -Name '(Default)' -Value $progId
}

# 3. Register the app for the "Open with" dialog (friendly name + supported types).
$appKey = "HKCU:\SOFTWARE\Classes\Applications\SeriousView.exe"
New-Item -Path $appKey -Force | Out-Null
Set-ItemProperty -Path $appKey -Name 'FriendlyAppName' -Value 'SeriousView'
New-Item -Path "$appKey\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$appKey\DefaultIcon" -Name '(Default)' -Value "$exe,0"
New-Item -Path "$appKey\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$appKey\shell\open\command" -Name '(Default)' -Value $cmd
New-Item -Path "$appKey\SupportedTypes" -Force | Out-Null
foreach ($ext in $Extensions) { Set-ItemProperty -Path "$appKey\SupportedTypes" -Name $ext -Value '' }

# 4. Clear any per-extension UserChoice that would override our default.
#    Win10 1803+ ACL-protects this key; .NET DeleteSubKey works on the user's own hive.
function Clear-UserChoice([string]$ext) {
    $sub = "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext"
    try {
        $parent = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($sub, $true)
        if ($parent) { $parent.DeleteSubKey('UserChoice', $false); $parent.Close() }
        return $true
    } catch { return $false }
}
$allCleared = $true
foreach ($ext in $Extensions) { if (-not (Clear-UserChoice $ext)) { $allCleared = $false } }

# 5. Refresh Explorer's association cache immediately.
$sig = @'
using System;
using System.Runtime.InteropServices;
public static class SeriousViewShellNotify {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern void SHChangeNotify(uint id, uint flags, IntPtr a, IntPtr b);
}
'@
try {
    Add-Type -TypeDefinition $sig -ErrorAction Stop
    [SeriousViewShellNotify]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
} catch {}

Write-Host "  Done -- double-click a $($Extensions -join '/') file to open it in SeriousView." -ForegroundColor Green
if (-not $allCleared) {
    Write-Host ''
    Write-Host '  Could not clear a UserChoice (Windows ACL). If double-click still opens' -ForegroundColor Yellow
    Write-Host '  the old app, set it once: right-click a .md -> Open with -> Choose another' -ForegroundColor Yellow
    Write-Host '  app -> SeriousView -> Always.' -ForegroundColor Yellow
}
Write-Host '  Note: the file/app icon is the default .NET one until a brand .ico lands (#9).' -ForegroundColor DarkGray
Write-Host ''
