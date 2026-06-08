# ============================================================================
# SeriousView -- Portable single-file build (self-contained, no runtime needed)
# ============================================================================
# Produces ONE SeriousView.exe in dist/ that runs on any Windows 10/11 machine
# of the target architecture WITHOUT an installed .NET runtime. This is the
# "portable" half of the distribution story; the installer pipeline (Velopack /
# NSIS + auto-update) is a later milestone.
#
# Why these flags:
#   --self-contained                 bundle the .NET runtime -> no install needed
#   PublishSingleFile                collapse managed assemblies into one .exe
#   IncludeNativeLibrariesForSelf... fold Avalonia/Skia native .dll into the .exe
#                                    (without this they sit loose next to it)
#   EnableCompressionInSingleFile    shrink the bundle (~30-40%), tiny cold-start cost
#   DebugType/DebugSymbols=none      keep .pdb out of the shipped file
# Trimming is intentionally OFF: Avalonia resolves XAML via reflection, so the
# linker would strip types it can't see and break the app at runtime.
#
# Usage:
#   .\build.ps1                 # Release, win-x64, compressed single-file
#   .\build.ps1 -Rid win-arm64  # build for ARM64 instead
#   .\build.ps1 -ReadyToRun     # AOT-precompile for faster cold start (bigger file)
#   .\build.ps1 -NoOpen         # don't open Explorer when finished
# ============================================================================

param(
    [string]$Rid = 'win-x64',
    [switch]$ReadyToRun,
    [switch]$NoOpen
)

chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

$root    = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$project = Join-Path $root 'src\SeriousView\SeriousView.csproj'
$outDir  = Join-Path $root 'dist'
$start   = Get-Date

Write-Host ''
Write-Host "  SeriousView -- portable single-file  ($Rid)" -ForegroundColor Cyan
Write-Host ''

# Fresh output every time so a stale exe can never be mistaken for a new build.
if (Test-Path -LiteralPath $outDir) { Remove-Item -LiteralPath $outDir -Recurse -Force }

$publishArgs = @(
    '-c', 'Release',
    '-r', $Rid,
    '--self-contained', 'true',
    '-o', $outDir,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none',
    '-p:DebugSymbols=false'
)
if ($ReadyToRun) { $publishArgs += '-p:PublishReadyToRun=true' }

Write-Host '  Publishing (first run restores the runtime pack -- can take a minute)...' -ForegroundColor DarkGray
dotnet publish $project @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host '  BUILD FAILED' -ForegroundColor Red
    exit $LASTEXITCODE
}

# Drop any stray .pdb the single-file bundler may have left behind.
Get-ChildItem -LiteralPath $outDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

$exe = Join-Path $outDir 'SeriousView.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "  ERROR: expected $exe was not produced." -ForegroundColor Red
    exit 1
}

$sizeMB = '{0:0.0} MB' -f ((Get-Item -LiteralPath $exe).Length / 1MB)
# Get-FileHash isn't available in every PowerShell host here, so hash via .NET.
$sha = & {
    $algo = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.IO.File]::ReadAllBytes($exe)
        ([BitConverter]::ToString($algo.ComputeHash($bytes)) -replace '-', '')
    } finally { $algo.Dispose() }
}
$dur    = (Get-Date) - $start
$time   = '{0}:{1:D2}' -f [math]::Floor($dur.TotalMinutes), $dur.Seconds

# Anything besides the exe means the single-file packing didn't fully fold in.
$extra = Get-ChildItem -LiteralPath $outDir -File | Where-Object { $_.Name -ne 'SeriousView.exe' }

Write-Host ''
Write-Host "  DONE  --  $time" -ForegroundColor Green
Write-Host "  $exe" -ForegroundColor White
Write-Host "  size    $sizeMB" -ForegroundColor Gray
Write-Host "  sha256  $sha" -ForegroundColor DarkGray
if ($extra) {
    Write-Host '  note: these files sit alongside the exe (not folded in):' -ForegroundColor Yellow
    $extra | ForEach-Object { Write-Host "    $($_.Name)" -ForegroundColor Yellow }
}
Write-Host ''

if (-not $NoOpen) {
    Start-Process explorer.exe -ArgumentList "/select,`"$exe`""
}
