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
#   .\build.ps1 -OutDir dist\win-x64   # alternate output dir (build_all puts each RID
#                                       in its own subfolder so phases don't clobber)
# ============================================================================

param(
    [string]$Rid = 'win-x64',
    [switch]$ReadyToRun,
    [switch]$NoOpen,
    [string]$OutDir = 'dist'
)

$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

# Shared console UI + helpers (UTF-8 console, box glyphs, Write-Banner/Step/Ok/...,
# Get-FileHashSHA256, Show-Notification). Keep ScriptKit.ps1 identical across repos.
. (Join-Path $root 'ScriptKit.ps1')

$project = Join-Path $root 'src\SeriousView\SeriousView.csproj'
$outDir  = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $root $OutDir }
$start   = Get-Date

Write-Banner "SeriousView -- portable single-file" "Release  $Rid  single-file  self-contained"

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

Write-Info 'Publishing (first run restores the runtime pack -- can take a minute)...'
dotnet publish $project @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Fail 'BUILD FAILED'
    Show-Notification -Title 'SeriousView build' -Body 'Publish failed' -IsError
    exit $LASTEXITCODE
}

# Drop any stray .pdb the single-file bundler may have left behind.
Get-ChildItem -LiteralPath $outDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

$exe = Join-Path $outDir 'SeriousView.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Fail "expected $exe was not produced."
    Show-Notification -Title 'SeriousView build' -Body 'Publish produced no exe' -IsError
    exit 1
}

$sizeMB = '{0:0.0} MB' -f ((Get-Item -LiteralPath $exe).Length / 1MB)
$sha    = Get-FileHashSHA256 -Path $exe
$dur    = (Get-Date) - $start
$time   = '{0}:{1:D2}' -f [math]::Floor($dur.TotalMinutes), $dur.Seconds

# Anything besides the exe means the single-file packing didn't fully fold in.
$extra = Get-ChildItem -LiteralPath $outDir -File | Where-Object { $_.Name -ne 'SeriousView.exe' }

Write-Host ''
Write-Ok "DONE  --  $time"
Write-Host ("    " + $SK_TM + " " + $exe)                 -ForegroundColor White
Write-Host ("    " + $SK_TM + " size    " + $sizeMB)      -ForegroundColor Gray
Write-Host ("    " + $SK_TM + " sha256  " + $sha)         -ForegroundColor DarkGray
if ($extra) {
    Write-Warn 'these files sit alongside the exe (not folded in):'
    $extra | ForEach-Object { Write-Info $_.Name }
}
Write-Host ''

Show-Notification -Title 'SeriousView build' -Body "Done in $time -- $sizeMB" -IconPath $exe

if (-not $NoOpen) {
    Start-Process explorer.exe -ArgumentList "/select,`"$exe`""
}
